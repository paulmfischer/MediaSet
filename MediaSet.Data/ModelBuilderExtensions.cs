using MediaSet.Data.MovieData;
using Microsoft.EntityFrameworkCore;

namespace MediaSet.Data
{
    public static class ModelBuilderExtensions
    {
        public static void SetupMediaGenreMapping(this ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MediaGenre>()
                .HasKey(mg => new { mg.GenreId, mg.MediaId });
            modelBuilder.Entity<MediaGenre>()
                .HasOne(mg => mg.Media)
                .WithMany(m => m.MediaGenres)
                .HasForeignKey(mg => mg.MediaId);
            modelBuilder.Entity<MediaGenre>()
                .HasOne(mg => mg.Genre)
                .WithMany(m => m.MediaGenres)
                .HasForeignKey(mg => mg.GenreId);
        }

        public static void SetupMovieStudioMapping(this ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MovieStudio>()
                .HasKey(mg => new { mg.MovieId, mg.StudioId });
            modelBuilder.Entity<MovieStudio>()
                .HasOne(mg => mg.Movie)
                .WithMany(m => m.MovieStudios)
                .HasForeignKey(mg => mg.MovieId);
            modelBuilder.Entity<MovieStudio>()
                .HasOne(mg => mg.Studio)
                .WithMany(m => m.MovieStudios)
                .HasForeignKey(mg => mg.StudioId);
        }
    }
}
