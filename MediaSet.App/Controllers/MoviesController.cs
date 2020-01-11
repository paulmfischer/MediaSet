using MediaSet.Data.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace MediaSet.App.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MoviesController : ControllerBase
    {
        private readonly IMoviesRepository moviesRepository;

        public MoviesController(IMoviesRepository moviesRepository)
        {
            this.moviesRepository = moviesRepository;
        }

        [Route("GetAll")]
        [HttpGet]
        public async Task<IActionResult> GetAll() => Ok(await this.moviesRepository.GetAll());

        [Route("{id}")]
        [HttpGet]
        public async Task<IActionResult> GetById(int id) => Ok(await this.moviesRepository.GetById(id));
    }
}