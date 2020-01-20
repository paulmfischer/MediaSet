using MediaSet.Data.GameData;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace MediaSet.Data.Repositories
{
    public class GamesRepository : BaseRepository<Game>, IGamesRepository
    {
        private readonly MediaSetContext context;

        public GamesRepository(MediaSetContext context)
        {
            this.context = context;
        }

        public override IQueryable<Game> GetBaseQuery()
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

        public override Task<int> GetTotalEntities()
        {
            return this.context.Games.CountAsync();
        }
    }
}
