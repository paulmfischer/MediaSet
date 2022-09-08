using Models;
using Services.UPC.Upcitemdb.Responses;

namespace Services.UPC.Upcitemdb;

public class UpcitemdbService : IUPCService
{

    private readonly HttpClient _httpClient;
    // private readonly string _remoteServiceBaseUrl;

    public UpcitemdbService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<UPCLookup>> Lookup(string UPC)
    {
        var url = $"{this._httpClient.BaseAddress?.AbsoluteUri}lookup?upc={UPC}";
        var response = await this._httpClient.GetFromJsonAsync<UpcitemdbResponse>(url);
        var lookups = response?.Items.Select(item => new UPCLookup
        {
            UPC = item.Upc,
            Description = item.Description,
            Title = item.Title
        });

        return lookups ?? new List<UPCLookup>();
    }
}