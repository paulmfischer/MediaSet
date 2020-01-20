using MediaSet.Data.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace MediaSet.App.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BooksController : ControllerBase
    {
        private readonly IBooksRepository booksRepository;

        public BooksController(IBooksRepository booksRepository)
        {
            this.booksRepository = booksRepository;
        }

        [Route("GetAll")]
        [HttpGet]
        public async Task<IActionResult> GetAll() =>Ok(await this.booksRepository.GetAll());

        [Route("paged")]
        [HttpGet]
        public async Task<IActionResult> Paged(int skip, int take, string filterValue) => Ok(await this.booksRepository.Paged(skip, take, filterValue));

        [Route("{id}")]
        [HttpGet]
        public async Task<IActionResult> GetById(int id) => Ok(await this.booksRepository.GetById(id));
    }
}