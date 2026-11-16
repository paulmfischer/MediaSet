namespace MediaSet.Api.Infrastructure.Lookup.Clients.GoogleBooks;

public interface IGoogleBooksClient
{
    /// <summary>
    /// Cover image URL of the Google Books volume whose identifiers contain <paramref name="isbn"/>; null when
    /// no volume matches the ISBN exactly, it has no cover, or Google Books refuses the request (403). Throws <see cref="ProviderLookupException"/> when
    /// Google Books fails (rate limit, server error) so the caller can retry later.
    /// </summary>
    Task<string?> GetCoverUrlByIsbnAsync(string isbn, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cover image URL of a Google Books volume whose title (with or without its subtitle) equals the stored title
    /// and that lists at least one of the stored <paramref name="authors"/> (when any are stored); null otherwise.
    /// Failure behavior matches <see cref="GetCoverUrlByIsbnAsync"/>.
    /// </summary>
    Task<string?> GetCoverUrlByTitleAsync(string title, string? subtitle, IReadOnlyList<string> authors,
        CancellationToken cancellationToken = default);
}
