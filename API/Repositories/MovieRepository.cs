using Entities;
using Microsoft.EntityFrameworkCore;

namespace Repositories;

public class MovieRepository : IMovieRepository
{
    private readonly MediaSetContext context;

    public MovieRepository(MediaSetContext context)
    {
        this.context = context;
    }

    public async Task<MovieEntity> Create(MovieEntity data)
    {
        this.context.Add(data);
        await this.context.SaveChangesAsync();

        return data;
    }

    public IEnumerable<MovieEntity> GetList()
    {
        return this.context.Movies.AsEnumerable();
    }

    public Task<MovieEntity?> Get(int id)
    {
        return this.context.Movies.SingleOrDefaultAsync(movie => movie.Id == id);
    }

    public async Task<MovieEntity> Update(MovieEntity data)
    {
        this.context.Movies.Update(data);
        await this.context.SaveChangesAsync();

        return data;
    }

    public async Task Delete(int id)
    {
        var movieToDelete = await this.context.Movies.FindAsync(id);

        if (movieToDelete == null)
        {
            return;
        }
        this.context.Movies.Remove(movieToDelete);
        await this.context.SaveChangesAsync();
    }
}