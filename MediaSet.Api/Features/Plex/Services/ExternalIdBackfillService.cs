using System.Net;
using System.Text.RegularExpressions;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Lookup.Clients.Tmdb;
using MediaSet.Api.Infrastructure.Plex;
using MediaSet.Api.Shared.Models;
using Microsoft.Extensions.Options;

namespace MediaSet.Api.Features.Plex.Services;

/// <summary>Counts of one backfill: ids saved, and movies searched but left blank.</summary>
public record ExternalIdBackfillResult(int Filled, int Skipped);

public interface IExternalIdBackfillService
{
    /// <summary>Fills empty movie ExternalIds from TMDB. Cancellation is the only exception it throws.</summary>
    Task<ExternalIdBackfillResult> RunAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Looks up movies without an ExternalId on TMDB by title and year. A wrong id is worse than a missing one,
/// so an id is saved only when exactly one result has the same title (case-insensitive) and release year.
/// A movie that already has an ExternalId is never selected. Throttled by <see cref="PlexBackfillConfiguration"/>,
/// not by the background image lookup settings. ITmdbClient is registered only when TMDB is configured.
/// </summary>
public sealed partial class ExternalIdBackfillService(
    IEntityService<Movie> movies,
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
        var candidates = (await movies.GetListAsync(cancellationToken))
            .Where(m => !string.IsNullOrEmpty(m.Id) && string.IsNullOrWhiteSpace(m.ExternalId) && !string.IsNullOrWhiteSpace(m.Title))
            .Take(Math.Max(1, config.BatchSize))
            .ToList();

        var filled = 0;
        var skipped = 0;
        DateTimeOffset? lastRequest = null;
        foreach (var movie in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var year = ParseYear(movie.ReleaseDate);
            if (year == null)
            {
                skipped++;
                continue;
            }

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
                var response = await tmdb.SearchMovieAsync(movie.Title.Trim(), cancellationToken);
                var matches = (response?.Results ?? [])
                    .Where(r => string.Equals(r.Title?.Trim(), movie.Title.Trim(), StringComparison.OrdinalIgnoreCase)
                        && ParseYear(r.ReleaseDate) == year)
                    .ToList();
                if (matches.Count != 1)
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

                current.ExternalId = ExternalIdFormat.ForTmdb(matches[0].Id);
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
                logger.LogWarning("ExternalId backfill could not process movie {MovieId} ({ExceptionType})", movie.Id, ex.GetType().Name);
            }
        }

        logger.LogInformation("ExternalId backfill finished: {Filled} filled, {Skipped} skipped", filled, skipped);
        return new ExternalIdBackfillResult(filled, skipped);
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
