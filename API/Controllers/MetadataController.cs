using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API.Data;

namespace API.Controllers;

[ApiController]
[Route("[controller]")]
public class MetadataController : ControllerBase
{
    private readonly MediaSetContext context;

    public MetadataController(MediaSetContext _context)
    {
        context = _context;
    }

    [HttpGet("Formats")]
    public async Task<ActionResult<List<Format>>> GetFormats()
    {
        return await context.Formats.ToListAsync();
    }
    
    [HttpGet("Genres")]
    public async Task<ActionResult<List<Genre>>> GetGenres()
    {
        return await context.Genres.ToListAsync();
    }

    [HttpGet("Studios")]
    public async Task<ActionResult<List<Studio>>> GetStudios()
    {
        return await context.Studios.ToListAsync();
    }
}