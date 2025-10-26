using MediaSet.Api.Clients;
using MediaSet.Api.Models;

namespace MediaSet.Api.Services.Lookup;

public class BookLookupStrategy : ILookupStrategy<BookResponse>
{
    private readonly IOpenLibraryClient _openLibraryClient;
    private readonly ILogger<BookLookupStrategy> _logger;

    public string EntityType => "books";

    public BookLookupStrategy(IOpenLibraryClient openLibraryClient, ILogger<BookLookupStrategy> logger)
    {
        _openLibraryClient = openLibraryClient;
        _logger = logger;
    }

    public bool SupportsIdentifierType(string identifierType)
    {
        return identifierType.ToLowerInvariant() switch
        {
            "isbn" => true,
            "lccn" => true,
            "oclc" => true,
            "olid" => true,
            _ => false
        };
    }

    public async Task<BookResponse?> LookupAsync(string identifierType, string identifierValue, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("BookLookupStrategy: Looking up {identifierType} {identifierValue}", identifierType, identifierValue);

        var result = await _openLibraryClient.GetReadableBookAsync(identifierType, identifierValue, cancellationToken);

        if (result != null)
        {
            _logger.LogInformation("BookLookupStrategy: Found book {title}", result.Title);
        }
        else
        {
            _logger.LogInformation("BookLookupStrategy: No book found for {identifierType} {identifierValue}", identifierType, identifierValue);
        }

        return result;
    }
}
