using MediaSet.Api.Infrastructure.Lookup.Models;
using MediaSet.Api.Shared.Models;
using MediaSet.Api.Infrastructure.Lookup.Clients.OpenLibrary;
using MediaSet.Api.Infrastructure.Lookup.Clients.UpcItemDb;
using Serilog;
using SerilogTracing;

namespace MediaSet.Api.Infrastructure.Lookup.Strategies;

public class BookLookupStrategy : ILookupStrategy<BookResponse>
{
    private readonly IOpenLibraryClient _openLibraryClient;
    private readonly IUpcItemDbClient _upcItemDbClient;
    private readonly ILogger<BookLookupStrategy> _logger;

    private static readonly IdentifierType[] _supportedIdentifierTypes = 
    [
        IdentifierType.Isbn,
        IdentifierType.Lccn,
        IdentifierType.Oclc,
        IdentifierType.Olid,
        IdentifierType.Upc,
        IdentifierType.Ean
    ];

    public BookLookupStrategy(
        IOpenLibraryClient openLibraryClient,
        IUpcItemDbClient upcItemDbClient,
        ILogger<BookLookupStrategy> logger)
    {
        _openLibraryClient = openLibraryClient;
        _upcItemDbClient = upcItemDbClient;
        _logger = logger;
    }

    public bool CanHandle(MediaTypes entityType, IdentifierType identifierType)
    {
        return entityType == MediaTypes.Books && _supportedIdentifierTypes.Contains(identifierType);
    }

    public async Task<BookResponse?> LookupAsync(
        IdentifierType identifierType, 
        string identifierValue, 
        CancellationToken cancellationToken)
    {
        using var activity = Log.Logger.StartActivity("BookLookup {IdentifierType}", new { IdentifierType = identifierType, identifierValue });
        
        _logger.LogInformation("Looking up book with {IdentifierType}: {IdentifierValue}", 
            identifierType, identifierValue);

        return identifierType switch
        {
            IdentifierType.Isbn => await LookupByIsbnAsync(identifierValue, cancellationToken),
            IdentifierType.Lccn => await LookupByLccnAsync(identifierValue, cancellationToken),
            IdentifierType.Oclc => await LookupByOclcAsync(identifierValue, cancellationToken),
            IdentifierType.Olid => await LookupByOlidAsync(identifierValue, cancellationToken),
            IdentifierType.Upc or IdentifierType.Ean => await LookupByUpcAsync(identifierValue, cancellationToken),
            _ => null
        };
    }

    private async Task<BookResponse?> LookupByIsbnAsync(string isbn, CancellationToken cancellationToken)
    {
        return await _openLibraryClient.GetReadableBookByIsbnAsync(isbn, cancellationToken);
    }

    private async Task<BookResponse?> LookupByLccnAsync(string lccn, CancellationToken cancellationToken)
    {
        return await _openLibraryClient.GetReadableBookByLccnAsync(lccn, cancellationToken);
    }

    private async Task<BookResponse?> LookupByOclcAsync(string oclc, CancellationToken cancellationToken)
    {
        return await _openLibraryClient.GetReadableBookByOclcAsync(oclc, cancellationToken);
    }

    private async Task<BookResponse?> LookupByOlidAsync(string olid, CancellationToken cancellationToken)
    {
        return await _openLibraryClient.GetReadableBookByOlidAsync(olid, cancellationToken);
    }

    private async Task<BookResponse?> LookupByUpcAsync(string upc, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Looking up UPC/EAN: {Upc} to find ISBN", upc);

        var upcResult = await _upcItemDbClient.GetItemByCodeAsync(upc, cancellationToken);
        
        if (upcResult == null || upcResult.Items.Count == 0)
        {
            _logger.LogWarning("No UPC/EAN data found for code: {Upc}", upc);
            return null;
        }

        var firstItem = upcResult.Items[0];
        
        if (!string.IsNullOrEmpty(firstItem.Isbn))
        {
            _logger.LogInformation("Found ISBN {Isbn} from UPC/EAN {Upc}, looking up book data", 
                firstItem.Isbn, upc);
            return await LookupByIsbnAsync(firstItem.Isbn, cancellationToken);
        }

        _logger.LogWarning("UPC/EAN {Upc} data does not contain ISBN", upc);
        return null;
    }
}
