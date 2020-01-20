using MediaSet.Data.MovieData;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace MediaSet.Data.Repositories
{
    public class MoviesRepository : BaseRepository<Movie>, IMoviesRepository
    {
        private readonly MediaSetContext context;

        public MoviesRepository(MediaSetContext context) : base(context)
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

        //public async Task<IList<Movie>> GetAll()
        //{
        //    return await this.GetBaseQuery().ToListAsync();
        //}

        //public async Task<Movie> GetById(int id)
        //{
        //    return await this.GetBaseQuery().FirstOrDefaultAsync(m => m.Id == id);
        //}

        //public async Task<PagedResult<Movie>> Paged(int skip, int take)
        //{
        //    return new PagedResult<Movie>
        //    {
        //        Items = await this.GetBaseQuery().Skip(skip).Take(take).ToListAsync(),
        //        Total = await this.context.Books.CountAsync()
        //    };
        //}

        //private IQueryable<Movie> GetBaseQuery()
        //{
        //    return this.context.Movies
        //        .Include(movie => movie.Studio)
        //        .Include(movie => movie.Media)
        //            .ThenInclude(media => media.Format)
        //        .Include(movie => movie.Media)
        //            .ThenInclude(media => media.MediaGenres)
        //                .ThenInclude(mg => mg.Genre)
        //        .Include(movie => movie.MovieDirectors)
        //            .ThenInclude(ma => ma.Director)
        //        .Include(movie => movie.MovieProducers)
        //            .ThenInclude(mp => mp.Producer)
        //        .Include(movie => movie.MovieWriters)
        //            .ThenInclude(mw => mw.Writer);
        //}
    }
}
