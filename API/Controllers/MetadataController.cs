using Microsoft.AspNetCore.Mvc;
using API.Data;

namespace API.Controllers;

[ApiController]
[Route("[controller]")]
public class MetadataController : ControllerBase
{
    private readonly MetadataService metadataService;

    public MetadataController(MetadataService _metadataService)
    {
        metadataService = _metadataService;
    }

    [HttpGet("Formats")]
    public async Task<ActionResult<List<Format>>> GetFormats()
    {
        return await metadataService.GetAll<Format>();
    }
    
    [HttpGet("Genres")]
    public async Task<ActionResult<List<Genre>>> GetGenres()
    {
        return await metadataService.GetAll<Genre>();
    }

    [HttpGet("Studios")]
    public async Task<ActionResult<List<Studio>>> GetStudios()
    {
        return await metadataService.GetAll<Studio>();
    }
}