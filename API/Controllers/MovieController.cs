
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API.Data;

namespace API.Controllers;

[ApiController]
[Route("[controller]")]
public class MovieController : ControllerBase
{
    private readonly MediaSetContext context;

    public MovieController(MediaSetContext _context)
    {
        context = _context;
    }

    [HttpGet()]
    public async Task<ActionResult<List<Movie>>> GetAllMovies()
    {
        return await context.Movies.ToListAsync();
    }
}