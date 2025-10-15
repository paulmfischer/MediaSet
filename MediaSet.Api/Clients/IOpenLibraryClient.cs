using MediaSet.Api.Models;

namespace MediaSet.Api.Clients;

public interface IOpenLibraryClient
{
  Task<BookResponse?> GetBookByIsbnAsync(string isbn);
  Task<BookResponse?> GetReadableBookAsync(string identifierType, string identifierValue);
  Task<BookResponse?> GetReadableBookByIsbnAsync(string isbn);
  Task<BookResponse?> GetReadableBookByLccnAsync(string lccn);
  Task<BookResponse?> GetReadableBookByOclcAsync(string oclc);
  Task<BookResponse?> GetReadableBookByOlidAsync(string olid);
  Task<BookResponse?> GetReadableBookAsync(IdentifierType identifierType, string identifierValue);
}
