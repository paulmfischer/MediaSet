using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace MediaSet.Data
{
    public class MediaSetContext : DbContext
    {
        public DbSet<Media> Media { get; set; }
        public DbSet<MediaType> MediaTypes { get; set; }
        public DbSet<Genre> Genres { get; set; }
        //public DbSet<Format> Formats { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseSqlite("Data Source=MediaSet.db");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
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
            //modelBuilder.Entity<BookCategory>()
            //    .HasOne(bc => bc.Category)
            //    .WithMany(c => c.BookCategories)
            //    .HasForeignKey(bc => bc.CategoryId);
        }
    }
}
