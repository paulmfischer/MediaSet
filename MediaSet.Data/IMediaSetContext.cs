using MediaSet.Data.MovieData;
using Microsoft.EntityFrameworkCore;

namespace MediaSet.Data
{
    public interface IMediaSetContext
    {
        public DbSet<Media> Media { get; set; }
        public DbSet<MediaType> MediaTypes { get; set; }
        public DbSet<Genre> Genres { get; set; }
        public DbSet<Format> Formats { get; set; }
        public DbSet<Studio> Studios { get; set; }
        public DbSet<Movie> Movies { get; set; }
    }
}
