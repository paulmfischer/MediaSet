using Models;

namespace Services.UPC;

public interface IUPCService
{
    Task<IEnumerable<UPCLookup>> Lookup(string UPC);
}