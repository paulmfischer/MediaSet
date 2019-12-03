using Microsoft.EntityFrameworkCore;
using MediaSet.Data.MovieData;

namespace MediaSet.Data
{
    public class MediaSetContext : DbContext, IMediaSetContext
    {
        public DbSet<Media> Media { get; set; }
        public DbSet<MediaType> MediaTypes { get; set; }
        public DbSet<Genre> Genres { get; set; }
        public DbSet<Format> Formats { get; set; }
        public DbSet<Studio> Studios { get; set; }
        public DbSet<Movie> Movies { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseSqlite("Data Source=MediaSet.db");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.SetupMediaGenreMapping();
            modelBuilder.SetupMovieStudioMapping();

            modelBuilder.Seed();
        }
    }
}
