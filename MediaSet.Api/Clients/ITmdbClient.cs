using MediaSet.Api.Models;

namespace MediaSet.Api.Clients;

public interface ITmdbClient
{
    Task<TmdbSearchResponse?> SearchMovieAsync(string title, CancellationToken cancellationToken);
    Task<TmdbMovieResponse?> GetMovieDetailsAsync(int movieId, CancellationToken cancellationToken);
}
