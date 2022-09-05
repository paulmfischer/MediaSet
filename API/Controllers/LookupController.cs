using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("[controller]")]
public class LookupController : ControllerBase
{
    [HttpGet("movie/{upc}")]
    public Task Lookup(string upc)
    {
        throw new NotImplementedException();
    }
}