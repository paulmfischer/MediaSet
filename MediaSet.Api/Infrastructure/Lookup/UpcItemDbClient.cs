using MediaSet.Api.Features.Lookup.Models;
using MediaSet.Api.Features.Entities.Models;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MediaSet.Api.Features.Entities.Models;

namespace MediaSet.Api.Infrastructure.Lookup;

public class UpcItemDbClient : IUpcItemDbClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UpcItemDbClient> _logger;
    private readonly UpcItemDbConfiguration _configuration;

    public UpcItemDbClient(
        HttpClient httpClient,
        IOptions<UpcItemDbConfiguration> configuration,
        ILogger<UpcItemDbClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration.Value;
    }

    public async Task<UpcItemResponse?> GetItemByCodeAsync(string code, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Looking up UPC/EAN code: {Code}", code);

            var response = await _httpClient.GetAsync($"prod/trial/lookup?upc={code}", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("UPCitemdb rate limit exceeded for code: {Code}", code);
                    throw new HttpRequestException("UPCitemdb rate limit exceeded", null, response.StatusCode);
                }

                _logger.LogWarning("UPCitemdb returned status code {StatusCode} for code: {Code}", 
                    response.StatusCode, code);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<UpcItemResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _logger.LogInformation("Successfully retrieved UPC/EAN data for code: {Code}, found {Count} items", 
                code, result?.Items.Count ?? 0);

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while looking up UPC/EAN code: {Code}", code);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up UPC/EAN code: {Code}", code);
            return null;
        }
    }
}
