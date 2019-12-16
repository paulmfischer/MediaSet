using MediaSet.Data.BookData;
using MediaSet.Data.GameData;
using MediaSet.Data.MovieData;
using Microsoft.EntityFrameworkCore;

namespace MediaSet.Data
{
    public interface IMediaSetContext
    {
        DbSet<Media> Media { get; set; }
        DbSet<MediaType> MediaTypes { get; set; }
        DbSet<Genre> Genres { get; set; }
        DbSet<Format> Formats { get; set; }
        DbSet<Studio> Studios { get; set; }
        DbSet<Movie> Movies { get; set; }
        DbSet<Book> Books { get; set; }
        DbSet<Author> Authors { get; set; }
        DbSet<Publisher> Publishers { get; set; }
        DbSet<Director> Directors { get; set; }
        DbSet<Producer> Producers { get; set; }
        DbSet<Writer> Writers { get; set; }
        DbSet<Platform> Platforms { get; set; }
        DbSet<Developer> Developers { get; set; }
        DbSet<Game> Games { get; set; }
    }
}
