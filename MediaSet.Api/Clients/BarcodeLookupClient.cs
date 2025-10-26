using System.Text.Json;
using System.Text.RegularExpressions;
using MediaSet.Api.Models;
using MediaSet.Api.Services;
using Microsoft.Extensions.Options;

namespace MediaSet.Api.Clients;

public partial class BarcodeLookupClient : IProductLookupClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BarcodeLookupClient> _logger;
    private readonly ICacheService _cacheService;
    private readonly BarcodeLookupConfiguration _config;

    [GeneratedRegex(@"^\d{12,13}$")]
    private static partial Regex BarcodeValidationRegex();

    public BarcodeLookupClient(
        HttpClient httpClient,
        ILogger<BarcodeLookupClient> logger,
        ICacheService cacheService,
        IOptions<BarcodeLookupConfiguration> config)
    {
        _httpClient = httpClient;
        _logger = logger;
        _cacheService = cacheService;
        _config = config.Value;
    }

    public void Dispose() => _httpClient?.Dispose();

    public async Task<ProductLookupResponse?> LookupBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            _logger.LogWarning("Barcode lookup called with empty barcode");
            return null;
        }

        if (!BarcodeValidationRegex().IsMatch(barcode))
        {
            _logger.LogWarning("Invalid barcode format: {barcode}", barcode);
            return null;
        }

        var cacheKey = $"barcode:{barcode}";
        var cached = await _cacheService.GetAsync<ProductLookupResponse>(cacheKey);
        if (cached != null)
        {
            _logger.LogInformation("Cache hit for barcode {barcode}", barcode);
            return cached;
        }

        try
        {
            var url = $"products?barcode={barcode}&key={_config.ApiKey}";
            var response = await _httpClient.GetFromJsonAsync<BarcodeLookupApiResponse>(url, cancellationToken);

            if (response?.Products == null || response.Products.Count == 0)
            {
                _logger.LogInformation("No product found for barcode {barcode}", barcode);
                // Cache negative result with a sentinel value or don't cache nulls
                return null;
            }

            var product = response.Products[0];
            var result = new ProductLookupResponse(
                Title: product.Title ?? string.Empty,
                Brand: product.Brand,
                Category: product.Category,
                Images: product.Images ?? [],
                RawJson: JsonSerializer.Serialize(product)
            );

            await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromDays(7));
            _logger.LogInformation("Successfully looked up barcode {barcode}: {title}", barcode, result.Title);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to lookup barcode {barcode}", barcode);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse barcode lookup response for {barcode}", barcode);
            return null;
        }
    }
}

internal record BarcodeLookupApiResponse(
    List<BarcodeLookupProduct> Products
);

internal record BarcodeLookupProduct(
    string? Title,
    string? Brand,
    string? Category,
    List<string>? Images
);
