using MediaSet.Data.Entities;
using Microsoft.EntityFrameworkCore;

public class MediaSetDbContext : DbContext
{
    public DbSet<Book> Books => Set<Book>();
    public DbSet<Format> Formats => Set<Format>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<Publisher> Publishers => Set<Publisher>();

    public MediaSetDbContext(DbContextOptions<MediaSetDbContext> options) : base(options)
    {}
}