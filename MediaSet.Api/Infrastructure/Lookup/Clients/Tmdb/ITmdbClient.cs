using MediaSet.Api.Features.Lookup.Models;
using MediaSet.Api.Features.Entities.Models;

namespace MediaSet.Api.Infrastructure.Lookup.Clients.Tmdb;

public interface ITmdbClient
{
    Task<TmdbSearchResponse?> SearchMovieAsync(string title, CancellationToken cancellationToken);
    Task<TmdbMovieResponse?> GetMovieDetailsAsync(int movieId, CancellationToken cancellationToken);
}
