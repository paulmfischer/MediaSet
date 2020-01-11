using MediaSet.Data.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace MediaSet.App.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GamesController : ControllerBase
    {
        private readonly IGamesRepository gamesRepository;

        public GamesController(IGamesRepository gamesRepository)
        {
            this.gamesRepository = gamesRepository;
        }

        [Route("GetAll")]
        [HttpGet]
        public async Task<IActionResult> GetAll() => Ok(await this.gamesRepository.GetAll());

        [Route("{id}")]
        [HttpGet]
        public async Task<IActionResult> GetById(int id) =>Ok(await this.gamesRepository.GetById(id));
    }
}