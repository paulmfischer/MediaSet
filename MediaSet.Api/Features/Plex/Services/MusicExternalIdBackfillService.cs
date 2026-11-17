using System.Net;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Lookup;
using MediaSet.Api.Infrastructure.Lookup.Clients.MusicBrainz;
using MediaSet.Api.Infrastructure.Lookup.Models;
using MediaSet.Api.Infrastructure.Plex;
using MediaSet.Api.Shared.Models;
using Microsoft.Extensions.Options;

namespace MediaSet.Api.Features.Plex.Services;

public interface IMusicExternalIdBackfillService
{
    /// <summary>Fills empty music ExternalIds from MusicBrainz. Cancellation is the only exception it throws.</summary>
    Task<ExternalIdBackfillResult> RunAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Looks up music entries without an ExternalId on MusicBrainz and saves the release-group id as <c>mbid:&lt;id&gt;</c>.
/// The barcode is tried first: releases that all belong to one release group are a clear match. A barcode with no result
/// falls back to the exact (case-insensitive) title plus exact artist, accepted only when the matching releases share one
/// release group. A barcode that finds several release groups is ambiguous, so it also falls back to the title and artist
/// search, but then only a match that is also one of the barcode's groups is taken. A barcode lookup that fails skips the
/// entry (no fallback). A wrong id is worse than a missing one, so anything unclear stays blank. An entry that already has
/// an ExternalId is never selected, and the id is written with an atomic update that sets only ExternalId and only while it
/// is still empty, so an id or another field edited meanwhile is never overwritten.
/// Throttled by <see cref="PlexBackfillConfiguration"/> (batch size, and requests per minute counted per MusicBrainz
/// request). Entries never or least recently searched go first, tracked in <see cref="IMusicExternalIdBackfillStore"/>.
/// </summary>
public sealed class MusicExternalIdBackfillService(
    IEntityService<Music> musics,
    IMusicExternalIdBackfillStore attempts,
    IMusicExternalIdWriter writer,
    IOptions<PlexConfiguration> options,
    ILogger<MusicExternalIdBackfillService> logger,
    IMusicBrainzClient musicBrainz,
    TimeProvider? timeProvider = null) : IMusicExternalIdBackfillService
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private DateTimeOffset? _lastRequest;

    public async Task<ExternalIdBackfillResult> RunAsync(CancellationToken cancellationToken)
    {
        var config = options.Value.Backfill;
        var interval = TimeSpan.FromSeconds(60d / Math.Max(1, config.RequestsPerMinute));
        _lastRequest = null;

        var eligible = (await musics.GetListAsync(cancellationToken))
            .Where(m => !string.IsNullOrEmpty(m.Id) && string.IsNullOrWhiteSpace(m.ExternalId)
                && (!string.IsNullOrWhiteSpace(m.Barcode)
                    || (!string.IsNullOrWhiteSpace(m.Title) && !string.IsNullOrWhiteSpace(m.Artist))))
            .ToList();

        // Never or least recently searched first (stable), so entries that never resolve rotate to the back.
        var lastAttempts = await attempts.GetAttemptsAsync(cancellationToken);
        var eligibleIds = eligible.Select(m => m.Id!).ToHashSet(StringComparer.Ordinal);
        await attempts.DeleteAsync([.. lastAttempts.Keys.Where(id => !eligibleIds.Contains(id))], cancellationToken);
        var candidates = eligible
            .OrderBy(m => lastAttempts.TryGetValue(m.Id!, out var at) ? at : DateTime.MinValue)
            .Take(Math.Max(1, config.BatchSize))
            .ToList();

        var filled = 0;
        var skipped = 0;
        foreach (var music in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var releaseGroupId = await FindReleaseGroupAsync(music, interval, cancellationToken);
                await RecordAttemptAsync(music.Id!, cancellationToken);

                if (releaseGroupId == null)
                {
                    skipped++;
                    continue;
                }

                // Sets only ExternalId, and only while it is still empty (false when an id was entered since or the album is gone)
                if (await writer.SetIfEmptyAsync(music.Id!, ExternalIdFormat.ForMusicBrainzReleaseGroup(releaseGroupId), cancellationToken))
                {
                    filled++;
                }
                else
                {
                    skipped++;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests } or ProviderRateLimitException)
            {
                // Rate limited: stop here, the rest waits for the next run
                logger.LogWarning("MusicBrainz rate limit reached; stopping the music ExternalId backfill early");
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                skipped++;
                await RecordAttemptAsync(music.Id!, cancellationToken);
                logger.LogWarning("Music ExternalId backfill could not process album {MusicId} ({ExceptionType})", music.Id, ex.GetType().Name);
            }
        }

        logger.LogDebug("Music ExternalId backfill finished: {Filled} filled, {Skipped} skipped", filled, skipped);
        return new ExternalIdBackfillResult(filled, skipped);
    }

    /// <summary>The single release-group id the album resolves to, or null when there is none or it is not clear.</summary>
    private async Task<string?> FindReleaseGroupAsync(Music music, TimeSpan interval, CancellationToken cancellationToken)
    {
        List<string> barcodeGroups = [];
        if (!string.IsNullOrWhiteSpace(music.Barcode))
        {
            await ThrottleAsync(interval, cancellationToken);
            var byBarcode = await musicBrainz.SearchByBarcodeAsync(music.Barcode.Trim(), cancellationToken);
            barcodeGroups = ReleaseGroupIds(byBarcode);
            if (barcodeGroups.Count == 1)
            {
                return barcodeGroups[0];
            }
            // None: fall through to the title search. Several: the barcode is ambiguous, the title search must pick one of them.
        }

        if (string.IsNullOrWhiteSpace(music.Title) || string.IsNullOrWhiteSpace(music.Artist))
        {
            return null;
        }

        var title = music.Title.Trim();
        var artist = music.Artist.Trim();
        await ThrottleAsync(interval, cancellationToken);
        var byName = await musicBrainz.SearchByEntityPropertiesAsync(
            new Dictionary<string, string> { ["title"] = title, ["artist"] = artist }, cancellationToken);
        var named = byName
            .Where(r => string.Equals(r.Title?.Trim(), title, StringComparison.OrdinalIgnoreCase)
                && r.ArtistCredit.Count == 1
                && string.Equals(r.ArtistCredit[0].Name?.Trim(), artist, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var namedGroups = ReleaseGroupIds(named);
        if (barcodeGroups.Count > 1)
        {
            namedGroups = [.. namedGroups.Where(g => barcodeGroups.Contains(g, StringComparer.OrdinalIgnoreCase))];
        }
        return namedGroups.Count == 1 ? namedGroups[0] : null;
    }

    private static List<string> ReleaseGroupIds(IEnumerable<MusicBrainzRelease> releases) =>
        [.. releases
            .Select(r => r.ReleaseGroup?.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>Waits so MusicBrainz requests are at least <paramref name="interval"/> apart.</summary>
    private async Task ThrottleAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        if (_lastRequest != null)
        {
            var wait = _lastRequest.Value + interval - _time.GetUtcNow();
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, _time, cancellationToken);
            }
        }
        _lastRequest = _time.GetUtcNow();
    }

    /// <summary>Remembers the search so the album goes behind untried ones next run. A store failure never fails the backfill.</summary>
    private async Task RecordAttemptAsync(string musicId, CancellationToken cancellationToken)
    {
        try
        {
            await attempts.RecordAttemptAsync(musicId, _time.GetUtcNow().UtcDateTime, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Could not record the music ExternalId backfill attempt for album {MusicId} ({ExceptionType})", musicId, ex.GetType().Name);
        }
    }
}
