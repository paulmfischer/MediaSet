using MediaSet.Data.GameData;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
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
            return await this.context.Games
                    .Include(game => game.Media)
                        .ThenInclude(media => media.Format)
                    .Include(game => game.Media)
                        .ThenInclude(media => media.MediaGenres)
                            .ThenInclude(mg => mg.Genre)
                    .Include(game => game.Developer)
                    .Include(game => game.Platform)
                    .Include(game => game.Publisher)
                    .ToListAsync();
        }

        public async Task<Game> GetById(int id)
        {
            return await this.context.Games
                    .Include(game => game.Media)
                        .ThenInclude(media => media.Format)
                    .Include(game => game.Media)
                        .ThenInclude(media => media.MediaGenres)
                            .ThenInclude(mg => mg.Genre)
                    .Include(game => game.Developer)
                    .Include(game => game.Platform)
                    .Include(game => game.Publisher)
                    .FirstOrDefaultAsync(game => game.Id == id);
        }
    }
}
