using Microsoft.EntityFrameworkCore;

namespace API.Data;

public class MovieService : IEntityService<Movie>
{
    private readonly MediaSetContext context;

    public MovieService(MediaSetContext _context)
    {
        context = _context;
    }

    public Task<List<Movie>> GetAll()
    {
        return context.Movies
            .Include(m => m.Genres)
            .Include(m => m.Studio)
            .Include(m => m.MediaItem)
            .ThenInclude(mi => mi.Format)
            .ToListAsync();
    }

    public Task<Movie?> GetById(int Id)
    {
        return context.Movies
            .Include(m => m.Genres)
            .Include(m => m.Studio)
            .Include(m => m.MediaItem)
            .ThenInclude(mi => mi.Format)
            .FirstOrDefaultAsync(movie => movie.Id == Id);
    }
}