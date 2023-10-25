using MediaSet.BookApi;
using Microsoft.EntityFrameworkCore;

public class MediaSetDbContext : DbContext
{
    public MediaSetDbContext(DbContextOptions<MediaSetDbContext> options) : base(options)
    {}

    public DbSet<Book> Books => Set<Book>();
}