using Microsoft.AspNetCore.Mvc;
using Models;
using Services.UPC;

namespace API.Controllers;

[ApiController]
[Route("[controller]")]
public class LookupController : ControllerBase
{
    private IUPCService upcService;

    public LookupController(IUPCService upcService)
    {
        this.upcService = upcService;
    }

    [HttpGet("movie/{upc}")]
    public Task<IEnumerable<UPCLookup>> Lookup(string upc)
    {
        return this.upcService.Lookup(upc);
    }
}