using System.Net;
using System.Text.RegularExpressions;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Lookup.Models;
using MediaSet.Api.Infrastructure.Lookup.Clients.Tmdb;
using MediaSet.Api.Infrastructure.Plex;
using MediaSet.Api.Shared.Models;
using Microsoft.Extensions.Options;

namespace MediaSet.Api.Features.Plex.Services;

/// <summary>Counts of one backfill: ids saved, and entries searched but left blank.</summary>
public record ExternalIdBackfillResult(int Filled, int Skipped);

public interface IExternalIdBackfillService
{
    /// <summary>Fills empty movie ExternalIds from TMDB. Cancellation is the only exception it throws.</summary>
    Task<ExternalIdBackfillResult> RunAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Looks up movies without an ExternalId on TMDB by title and year. A wrong id is worse than a missing one,
/// so an id is saved only when exactly one result has the same title (case-insensitive) and release year.
/// TV series entries (<c>IsTvSeries</c>) are searched on TMDB TV by their title with the season text removed
/// (<see cref="PlexTvTitleParser.CleanTitle"/>), and need no year: a lone result, or a single result whose name equals
/// the cleaned title, is taken; when several names match, the year (when known) is used only to break the tie, and a
/// tie that remains leaves the field blank.
/// A movie that already has an ExternalId is never selected. Throttled by <see cref="PlexBackfillConfiguration"/>,
/// not by the background image lookup settings. Movies without a parseable year are never candidates (TV entries need only a title). The last search time
/// of each movie is kept in <see cref="IExternalIdBackfillStore"/> so unresolved movies rotate behind untried ones. ITmdbClient is registered only when TMDB is configured.
/// </summary>
public sealed partial class ExternalIdBackfillService(
    IEntityService<Movie> movies,
    IExternalIdBackfillStore attempts,
    IOptions<PlexConfiguration> options,
    ILogger<ExternalIdBackfillService> logger,
    ITmdbClient? tmdb = null,
    TimeProvider? timeProvider = null) : IExternalIdBackfillService
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    public async Task<ExternalIdBackfillResult> RunAsync(CancellationToken cancellationToken)
    {
        if (tmdb == null)
        {
            logger.LogInformation("TMDB is not configured; skipping the Plex sync ExternalId backfill");
            return new ExternalIdBackfillResult(0, 0);
        }

        var config = options.Value.Backfill;
        var interval = TimeSpan.FromSeconds(60d / Math.Max(1, config.RequestsPerMinute));

        // Movies without a parseable year are not candidates: they cost no search and are not counted
        var eligible = (await movies.GetListAsync(cancellationToken))
            .Where(m => !string.IsNullOrEmpty(m.Id) && string.IsNullOrWhiteSpace(m.ExternalId) && !string.IsNullOrWhiteSpace(m.Title)
                && (m.IsTvSeries ? PlexTvTitleParser.CleanTitle(m.Title).Length > 0 : ParseYear(m.ReleaseDate) != null))
            .ToList();

        // Never or least recently searched first (stable), so movies that never resolve rotate to the back
        // and cannot starve the rest. Entries of movies no longer eligible (deleted, got an id) are purged.
        var lastAttempts = await attempts.GetAttemptsAsync(cancellationToken);
        var eligibleIds = eligible.Select(m => m.Id!).ToHashSet(StringComparer.Ordinal);
        await attempts.DeleteAsync([.. lastAttempts.Keys.Where(id => !eligibleIds.Contains(id))], cancellationToken);
        var candidates = eligible
            .OrderBy(m => lastAttempts.TryGetValue(m.Id!, out var at) ? at : DateTime.MinValue)
            .Take(Math.Max(1, config.BatchSize))
            .ToList();

        var filled = 0;
        var skipped = 0;
        DateTimeOffset? lastRequest = null;
        foreach (var movie in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var year = ParseYear(movie.ReleaseDate);

            if (lastRequest != null)
            {
                var wait = lastRequest.Value + interval - _time.GetUtcNow();
                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, _time, cancellationToken);
                }
            }
            lastRequest = _time.GetUtcNow();

            try
            {
                int? tmdbId;
                if (movie.IsTvSeries)
                {
                    var cleaned = PlexTvTitleParser.CleanTitle(movie.Title);
                    var response = await tmdb.SearchTvAsync(cleaned, cancellationToken);
                    await RecordAttemptAsync(movie.Id!, cancellationToken);
                    tmdbId = PickShow(response?.Results ?? [], cleaned, year);
                }
                else
                {
                    var response = await tmdb.SearchMovieAsync(movie.Title.Trim(), cancellationToken);
                    await RecordAttemptAsync(movie.Id!, cancellationToken);
                    var matches = (response?.Results ?? [])
                        .Where(r => string.Equals(r.Title?.Trim(), movie.Title.Trim(), StringComparison.OrdinalIgnoreCase)
                            && ParseYear(r.ReleaseDate) == year)
                        .ToList();
                    tmdbId = matches.Count == 1 ? matches[0].Id : null;
                }

                if (tmdbId == null)
                {
                    skipped++;
                    continue;
                }

                // Re-read so only ExternalId changes and an id set since the list was read is kept
                var current = await movies.GetAsync(movie.Id!, cancellationToken);
                if (current == null || !string.IsNullOrWhiteSpace(current.ExternalId))
                {
                    skipped++;
                    continue;
                }

                current.ExternalId = ExternalIdFormat.ForTmdb(tmdbId.Value);
                await movies.UpdateAsync(movie.Id!, current, cancellationToken);
                filled++;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
            {
                // Rate limited: stop here, the rest waits for the next run
                logger.LogWarning("TMDB rate limit reached; stopping the ExternalId backfill early");
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                skipped++;
                await RecordAttemptAsync(movie.Id!, cancellationToken);
                logger.LogWarning("ExternalId backfill could not process movie {MovieId} ({ExceptionType})", movie.Id, ex.GetType().Name);
            }
        }

        logger.LogDebug("ExternalId backfill finished: {Filled} filled, {Skipped} skipped", filled, skipped);
        return new ExternalIdBackfillResult(filled, skipped);
    }

    /// <summary>
    /// The confident TMDB show for a cleaned title, or null. Only results whose name equals the title (case-insensitive)
    /// are candidates, so a fuzzy hit on an unrelated show is never saved. One candidate is taken; several are narrowed
    /// by first air year, when the entry has one, and taken only if exactly one is left.
    /// </summary>
    private static int? PickShow(IReadOnlyList<TmdbTvSearchResult> results, string cleanedTitle, int? year)
    {
        var named = results
            .Where(r => string.Equals(r.Name?.Trim(), cleanedTitle.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (named.Count > 1 && year != null)
        {
            named = [.. named.Where(r => ParseYear(r.FirstAirDate) == year)];
        }

        return named.Count == 1 ? named[0].Id : null;
    }

    /// <summary>Remembers the search so the movie goes behind untried ones next run. A store failure never fails the backfill.</summary>
    private async Task RecordAttemptAsync(string movieId, CancellationToken cancellationToken)
    {
        try
        {
            await attempts.RecordAttemptAsync(movieId, _time.GetUtcNow().UtcDateTime, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Could not record the ExternalId backfill attempt for movie {MovieId} ({ExceptionType})", movieId, ex.GetType().Name);
        }
    }

    private static int? ParseYear(string? date)
    {
        if (string.IsNullOrWhiteSpace(date))
        {
            return null;
        }

        var match = YearPattern().Match(date);
        return match.Success ? int.Parse(match.Value) : null;
    }

    [GeneratedRegex(@"\b\d{4}\b")]
    private static partial Regex YearPattern();
}
