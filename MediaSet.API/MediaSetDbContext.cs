using MediaSet.BookApi;
using Microsoft.EntityFrameworkCore;

public class MediaSetDbContext : DbContext
{
    public DbSet<Book> Books => Set<Book>();

    public MediaSetDbContext(DbContextOptions<MediaSetDbContext> options) : base(options)
    {}
}