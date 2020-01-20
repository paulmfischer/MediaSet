using MediaSet.Data.MovieData;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace MediaSet.Data.Repositories
{
    public class MoviesRepository : BaseRepository<Movie>, IMoviesRepository
    {
        private readonly MediaSetContext context;

        public MoviesRepository(MediaSetContext context)
        {
            this.context = context;
        }

        public override IQueryable<Movie> GetBaseQuery()
        {
            return this.context.Movies
                .Include(movie => movie.Studio)
                .Include(movie => movie.Media)
                    .ThenInclude(media => media.Format)
                .Include(movie => movie.Media)
                    .ThenInclude(media => media.MediaGenres)
                        .ThenInclude(mg => mg.Genre)
                .Include(movie => movie.MovieDirectors)
                    .ThenInclude(ma => ma.Director)
                .Include(movie => movie.MovieProducers)
                    .ThenInclude(mp => mp.Producer)
                .Include(movie => movie.MovieWriters)
                    .ThenInclude(mw => mw.Writer);
        }

        public override Task<int> GetTotalEntities()
        {
            return this.context.Movies.CountAsync();
        }

        public override IQueryable<Movie> SearchEntityQuery(string filterValue)
        {
            return string.IsNullOrEmpty(filterValue) ? this.GetBaseQuery() : this.GetBaseQuery().Where(x => x.Media.Title.Contains(filterValue));
        }
    }
}
