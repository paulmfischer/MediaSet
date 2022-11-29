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
}