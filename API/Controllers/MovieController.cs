using Microsoft.AspNetCore.Mvc;
using API.Data;
using System.ComponentModel.DataAnnotations;

namespace API.Controllers;

[ApiController]
[Route("[controller]")]
public class MovieController : ControllerBase
{
    private readonly IEntityService<Movie> movieService;

    public MovieController(IEntityService<Movie> _movieService)
    {
        movieService = _movieService;
    }

    [HttpGet()]
    public async Task<ActionResult<List<Movie>>> GetAllMovies()
    {
        return await movieService.GetAll();
    }

    [HttpGet("{Id}")]
    public async Task<ActionResult<Movie>> GetById([Required] int Id)
    {
        var movie = await movieService.GetById(Id);

        if (movie == null)
        {
            return NotFound();
        }

        return movie;
    }
}