using Microsoft.EntityFrameworkCore;

namespace API.Data;

public class MetadataService
{
    private readonly MediaSetContext context;

    public MetadataService(MediaSetContext _context)
    {
        context = _context;
    }

    public Task<List<T>> GetAll<T>() where T : class
    {
        return context.Set<T>().ToListAsync();
    }

    public async Task<T> Create<T>(T entity) where T : class
    {
        context.Add<T>(entity);
        await context.SaveChangesAsync();

        return entity;
    }

    public T? GetById<T>(int Id) where T : class
    {
        return context.Find<T>(Id);
    }
}