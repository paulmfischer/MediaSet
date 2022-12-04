using Microsoft.EntityFrameworkCore;

namespace API;

public class MediaSetContext : DbContext
{
    public MediaSetContext(DbContextOptions<MediaSetContext> options) : base(options) { }

    public DbSet<Movie> Movies { get; set; } = null!;
    public DbSet<Format> Formats { get; set; } = null!;
    public DbSet<Genre> Genres { get; set; } = null!;
    public DbSet<Studio> Studios { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Movie>()
            .HasMany(movie => movie.Genres)
            .WithMany();
    }
}
