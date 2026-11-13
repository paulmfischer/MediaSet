namespace MediaSet.Api.Infrastructure.Lookup.Clients.GoogleBooks;

public interface IGoogleBooksClient
{
    /// <summary>
    /// Cover image URL of the Google Books volume whose identifiers contain <paramref name="isbn"/>; null when
    /// no volume matches the ISBN exactly or it has no cover. Throws <see cref="ProviderLookupException"/> when
    /// Google Books fails (rate limit, server error) so the caller can retry later.
    /// </summary>
    Task<string?> GetCoverUrlByIsbnAsync(string isbn, CancellationToken cancellationToken = default);
}
