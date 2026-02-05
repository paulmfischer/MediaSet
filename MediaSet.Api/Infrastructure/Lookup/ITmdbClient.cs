using MediaSet.Api.Features.Entities.Models;
using MediaSet.Api.Models;

namespace MediaSet.Api.Infrastructure.Lookup;

public interface ITmdbClient
{
    Task<TmdbSearchResponse?> SearchMovieAsync(string title, CancellationToken cancellationToken);
    Task<TmdbMovieResponse?> GetMovieDetailsAsync(int movieId, CancellationToken cancellationToken);
}
