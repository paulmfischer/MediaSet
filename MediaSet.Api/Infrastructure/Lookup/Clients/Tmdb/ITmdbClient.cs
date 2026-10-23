using MediaSet.Api.Infrastructure.Lookup.Models;

namespace MediaSet.Api.Infrastructure.Lookup.Clients.Tmdb;

public interface ITmdbClient
{
    Task<TmdbSearchResponse?> SearchMovieAsync(string title, CancellationToken cancellationToken);
    Task<TmdbMovieResponse?> GetMovieDetailsAsync(int movieId, CancellationToken cancellationToken);
    Task<TmdbTvSearchResponse?> SearchTvAsync(string title, CancellationToken cancellationToken);
    Task<TmdbTvResponse?> GetTvDetailsAsync(int seriesId, CancellationToken cancellationToken);
    Task<TmdbSeasonResponse?> GetSeasonDetailsAsync(int seriesId, int seasonNumber, CancellationToken cancellationToken);
}
