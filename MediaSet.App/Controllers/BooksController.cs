using System.Linq;
using System.Threading.Tasks;
using MediaSet.Data;
using MediaSet.Data.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        public async Task<IActionResult> GetAll()
        {
            return Ok(await this.booksRepository.GetAll());
        }

        [Route("{id}")]
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            return Ok(await this.booksRepository.GetById(id));
        }
    }
}