﻿using MediaSet.Data.MovieData;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MediaSet.Data.Repositories
{
    public class MoviesRepository : IMoviesRepository
    {
        private readonly MediaSetContext context;

        public MoviesRepository(MediaSetContext context)
        {
            this.context = context;
        }

        public async Task<IList<Movie>> GetAll()
        {
            return await this.context.Movies
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
                    .ThenInclude(mw => mw.Writer)
                .ToListAsync();
        }

        public async Task<Movie> GetById(int id)
        {
            return await this.context.Movies
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
                    .ThenInclude(mw => mw.Writer)
                .FirstOrDefaultAsync(m => m.Id == id);
        }
    }
}