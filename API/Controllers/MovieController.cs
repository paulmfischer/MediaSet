using Microsoft.AspNetCore.Mvc;
using Models;
using Services;

namespace API.Controllers;

[ApiController]
[Route("[controller]")]
public class MovieController : ControllerBase
{
    private readonly ILogger<MovieController> logger;
    private readonly IMovieService movieService;

    public MovieController(ILogger<MovieController> logger, IMovieService movieRepository)
    {
        this.logger = logger;
        this.movieService = movieRepository;
    }

    [HttpGet()]
    public IEnumerable<MovieModel> GetMovies()
    {
        return this.movieService.GetList();
    }

    [HttpGet("{id}")]
    public Task<MovieModel?> GetMovie(int id)
    {
        return this.movieService.Get(id);
    }


    [HttpPost]
    public Task<MovieModel> Create(CreateMovie newMovie)
    {
        return this.movieService.Create(newMovie);
    }

    [HttpPut]
    public Task<MovieModel> Update(MovieModel updateMovie)
    {
        return this.movieService.Update(updateMovie);
    }

    [HttpDelete]
    public Task Delete(int id)
    {
        return this.movieService.Delete(id);
    }
}
