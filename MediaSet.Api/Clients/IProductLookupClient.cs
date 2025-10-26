namespace MediaSet.Api.Clients;

public record ProductLookupResponse(
    string Title,
    string? Brand,
    string? Category,
    List<string> Images,
    string RawJson
);

public interface IProductLookupClient
{
    Task<ProductLookupResponse?> LookupBarcodeAsync(string barcode, CancellationToken cancellationToken = default);
}
