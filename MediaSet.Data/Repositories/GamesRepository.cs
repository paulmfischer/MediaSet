using MediaSet.Data.GameData;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MediaSet.Data.Repositories
{
    public class GamesRepository : IGamesRepository
    {
        private readonly MediaSetContext context;

        public GamesRepository(MediaSetContext context)
        {
            this.context = context;
        }

        public async Task<IList<Game>> GetAll()
        {
            return await this.GetBaseQuery().ToListAsync();
        }

        public async Task<Game> GetById(int id)
        {
            return await this.GetBaseQuery().FirstOrDefaultAsync(game => game.Id == id);
        }

        public async Task<PagedResult<Game>> Paged(int skip, int take)
        {
            return new PagedResult<Game>
            {
                Items = await this.GetBaseQuery().Skip(skip).Take(take).ToListAsync(),
                Total = await this.context.Books.CountAsync()
            };
        }

        private IQueryable<Game> GetBaseQuery()
        {
            return this.context.Games
                    .Include(game => game.Media)
                        .ThenInclude(media => media.Format)
                    .Include(game => game.Media)
                        .ThenInclude(media => media.MediaGenres)
                            .ThenInclude(mg => mg.Genre)
                    .Include(game => game.Developer)
                    .Include(game => game.Platform)
                    .Include(game => game.Publisher);
        }
    }
}
