using MediaSet.Api.Infrastructure.Lookup.Models;
using MediaSet.Api.Shared.Models;

namespace MediaSet.Api.Infrastructure.Lookup.Clients.OpenLibrary;

public interface IOpenLibraryClient
{
    Task<BookResponse?> GetBookByIsbnAsync(string isbn, CancellationToken cancellationToken = default);
    Task<BookResponse?> GetReadableBookAsync(string identifierType, string identifierValue, CancellationToken cancellationToken = default);
    Task<BookResponse?> GetReadableBookByIsbnAsync(string isbn, CancellationToken cancellationToken = default);
    Task<BookResponse?> GetReadableBookByLccnAsync(string lccn, CancellationToken cancellationToken = default);
    Task<BookResponse?> GetReadableBookByOclcAsync(string oclc, CancellationToken cancellationToken = default);
    Task<BookResponse?> GetReadableBookByOlidAsync(string olid, CancellationToken cancellationToken = default);
    Task<BookResponse?> GetReadableBookAsync(IdentifierType identifierType, string identifierValue, CancellationToken cancellationToken = default);
}
