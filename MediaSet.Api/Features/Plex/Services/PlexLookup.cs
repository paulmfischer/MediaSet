using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Infrastructure.Plex;
using MediaSet.Api.Shared.Models;

namespace MediaSet.Api.Features.Plex.Services;

public class PlexLookup(PlexEnablement enablement, IPlexItemStore items, ILogger<PlexLookup> logger) : IPlexLookup
{
    public async Task<object?> GetMovieDataAsync(string movieId, CancellationToken cancellationToken)
    {
        // Plex documents are keyed by ObjectId; any other id cannot be linked
        if (!enablement.IsEnabled(PlexEntityTypes.Movies) || !MongoDB.Bson.ObjectId.TryParse(movieId, out _))
        {
            return null;
        }

        // Plex data is a bonus on the lookup: a store failure must not fail the movie request
        try
        {
            var data = await items.GetAsync(movieId, cancellationToken);
            return data == null ? null : PlexItemDataDto.From(data).WithoutFilePath();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not read the Plex data of movie {MovieId}; returning the movie without it", movieId);
            return null;
        }
    }

    public async Task<IReadOnlyDictionary<string, object>> GetMovieSummariesAsync(IReadOnlyCollection<string> movieIds, CancellationToken cancellationToken)
    {
        var ids = movieIds.Where(id => MongoDB.Bson.ObjectId.TryParse(id, out _)).Distinct().ToList();
        if (ids.Count == 0 || !enablement.IsEnabled(PlexEntityTypes.Movies))
        {
            return new Dictionary<string, object>();
        }

        // The summaries are a bonus on the list: a store failure must not fail the search
        try
        {
            var docs = await items.ListSummariesAsync(ids, cancellationToken);
            return docs.ToDictionary(d => d.Id, d => (object)PlexListSummary.From(d));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not read the Plex summaries of {Count} movies; returning the list without them", ids.Count);
            return new Dictionary<string, object>();
        }
    }

    public async Task<IdFilter?> GetMovieFilterAsync(string filter, CancellationToken cancellationToken)
    {
        var normalized = PlexListFilters.Normalize(filter)
            ?? throw new ArgumentException($"Invalid plex filter '{filter}'. Use onPlex, notOnPlex, unwatched or broken.");
        if (!enablement.IsEnabled(PlexEntityTypes.Movies))
        {
            return null;
        }

        var ids = await items.ListIdsByFilterAsync(normalized, cancellationToken);
        return new IdFilter(ids, Exclude: normalized == PlexListFilters.NotOnPlex);
    }

    public async Task<PlexStats?> GetMovieStatsAsync(int totalMovies, CancellationToken cancellationToken)
    {
        if (!enablement.IsEnabled(PlexEntityTypes.Movies))
        {
            return null;
        }

        // Plex statistics are a bonus on the dashboard: a store failure must not fail the request
        try
        {
            var docs = await items.ListStatsAsync(cancellationToken);
            var onPlex = docs.Where(d => d.OnPlex).ToList();
            var watched = onPlex.Count(d => d.Watched);
            var resolutions = onPlex
                .GroupBy(d => string.IsNullOrWhiteSpace(d.Resolution) ? "unknown" : d.Resolution!.Trim())
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count());

            return new PlexStats(
                onPlex.Count,
                Math.Max(0, totalMovies - onPlex.Count),
                watched,
                onPlex.Count - watched,
                resolutions,
                Math.Max(0, totalMovies - docs.Count) + docs.Count(d => d.Broken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not read the Plex statistics; returning the statistics without them");
            return null;
        }
    }
}
