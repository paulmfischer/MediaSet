using Microsoft.EntityFrameworkCore;

namespace API.Data;

public class MediaSetContext : DbContext
{
    public MediaSetContext(DbContextOptions<MediaSetContext> options) : base(options)
    {}

    public DbSet<MediaItem> MediaItems { get; set; } = null!;
    public DbSet<Movie> Movies { get; set; } = null!;
    public DbSet<Format> Formats { get; set; } = null!;
    public DbSet<Genre> Genres { get; set; } = null!;
    public DbSet<Studio> Studios { get; set; } = null!;
}