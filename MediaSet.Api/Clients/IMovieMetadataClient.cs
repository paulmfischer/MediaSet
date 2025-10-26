namespace MediaSet.Api.Clients;

public record MovieMetadataResponse(
    string Title,
    List<string> Genres,
    List<string> ProductionCompanies,
    string Overview,
    string ReleaseDate,
    int? Runtime,
    string? Certification,
    bool IsTvSeries
);

public interface IMovieMetadataClient
{
    Task<MovieMetadataResponse?> SearchAndGetDetailsAsync(string title, int? year = null, CancellationToken cancellationToken = default);
}
