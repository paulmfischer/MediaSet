﻿using MediaSet.Data.BookData;
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

        public static void SetupMovieMappings(this ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MovieDirector>()
                .HasKey(mg => new { mg.MovieId, mg.DirectorId });
            modelBuilder.Entity<MovieDirector>()
                .HasOne(mg => mg.Movie)
                .WithMany(m => m.MovieDirectors)
                .HasForeignKey(mg => mg.MovieId);
            modelBuilder.Entity<MovieDirector>()
                .HasOne(mg => mg.Director)
                .WithMany(m => m.MovieDirectors)
                .HasForeignKey(mg => mg.DirectorId);

            modelBuilder.Entity<MovieProducer>()
                .HasKey(mg => new { mg.MovieId, mg.ProducerId });
            modelBuilder.Entity<MovieProducer>()
                .HasOne(mg => mg.Movie)
                .WithMany(m => m.MovieProducers)
                .HasForeignKey(mg => mg.MovieId);
            modelBuilder.Entity<MovieProducer>()
                .HasOne(mg => mg.Producer)
                .WithMany(m => m.MovieProducers)
                .HasForeignKey(mg => mg.ProducerId);

            modelBuilder.Entity<MovieWriter>()
                .HasKey(mg => new { mg.MovieId, mg.WriterId });
            modelBuilder.Entity<MovieWriter>()
                .HasOne(mg => mg.Movie)
                .WithMany(m => m.MovieWriters)
                .HasForeignKey(mg => mg.MovieId);
            modelBuilder.Entity<MovieWriter>()
                .HasOne(mg => mg.Writer)
                .WithMany(m => m.MovieWriters)
                .HasForeignKey(mg => mg.WriterId);
        }

        public static void SetupBookMappings(this ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BookAuthor>()
                .HasKey(ba => new { ba.BookId, ba.AuthorId });
            modelBuilder.Entity<BookAuthor>()
                .HasOne(ba => ba.Book)
                .WithMany(b => b.BookAuthors)
                .HasForeignKey(ba => ba.BookId);
            modelBuilder.Entity<BookAuthor>()
                .HasOne(ba => ba.Author)
                .WithMany(b => b.BookAuthors)
                .HasForeignKey(ba => ba.AuthorId);

            modelBuilder.Entity<BookPublisher>()
                .HasKey(ba => new { ba.BookId, ba.PublisherId });
            modelBuilder.Entity<BookPublisher>()
                .HasOne(ba => ba.Book)
                .WithMany(b => b.BookPublishers)
                .HasForeignKey(ba => ba.BookId);
            modelBuilder.Entity<BookPublisher>()
                .HasOne(ba => ba.Publisher)
                .WithMany(b => b.BookPublishers)
                .HasForeignKey(ba => ba.PublisherId);
        }

        public static void Seed(this ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MediaType>().HasData(
                new MediaType { Id = 1, Name = "Books" },
                new MediaType { Id = 2, Name = "Movies" },
                new MediaType { Id = 3, Name = "Games" }
            );

            //modelBuilder.Entity<Format>().HasData(
            //    new Format { Id = 1, Name = "DVD", MediaTypeId = 2 },
            //    new Format { Id = 2, Name = "Blu-ray", MediaTypeId = 2 },
            //    new Format { Id = 3, Name = "Hardcover", MediaTypeId = 1 },
            //    new Format { Id = 4, Name = "Softcover", MediaTypeId = 1 },
            //    new Format { Id = 5, Name = "Trade Paperback", MediaTypeId = 1 },
            //    new Format { Id = 6, Name = "eBook", MediaTypeId = 1 },
            //    new Format { Id = 7, Name = "PC", MediaTypeId = 3 },
            //    new Format { Id = 8, Name = "Xbox", MediaTypeId = 3 },
            //    new Format { Id = 9, Name = "Xbox 360", MediaTypeId = 3 },
            //    new Format { Id = 10, Name = "Xbox One", MediaTypeId = 3 },
            //    new Format { Id = 11, Name = "Playstation", MediaTypeId = 3 },
            //    new Format { Id = 12, Name = "Playstation 2", MediaTypeId = 3 },
            //    new Format { Id = 13, Name = "Playstation 3", MediaTypeId = 3 },
            //    new Format { Id = 14, Name = "Playstation 4", MediaTypeId = 3 },
            //    new Format { Id = 15, Name = "SNES", MediaTypeId = 3 },
            //    new Format { Id = 16, Name = "Nintendo Switch", MediaTypeId = 3 },
            //    new Format { Id = 17, Name = "Paperback", MediaTypeId = 1 }
            //);
        }
    }
}
