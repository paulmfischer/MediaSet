using MediaSet.Data.Entities;
using Microsoft.EntityFrameworkCore;

public class MediaSetDbContext : DbContext
{
    public DbSet<Book> Books => Set<Book>();
    public DbSet<Format> Formats => Set<Format>();

    public MediaSetDbContext(DbContextOptions<MediaSetDbContext> options) : base(options)
    {}
}