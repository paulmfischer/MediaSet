using MediaSet.Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace MediaSet.Data
{
    public static class MediaSetModelBuilderExtensions
    {
        public static void SetupBookAuthorMapping(this ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BookAuthor>()
                .HasKey(ba => new { ba.BookId, ba.AuthorId });

            modelBuilder.Entity<BookAuthor>()
                .HasOne(ba => ba.Book)
                .WithMany(b => b.BookAuthors)
                .HasForeignKey(ba => ba.BookId);

            modelBuilder.Entity<BookAuthor>()
                .HasOne(ba => ba.Author)
                .WithMany(a => a.BookAuthors)
                .HasForeignKey(ba => ba.AuthorId);
        }

        public static void Seed(this ModelBuilder modelBuilder)
        {
            //using (var context = new MediaSetDbContext())
            //{
            //    context.Database.EnsureCreated();

            //    context.Formats.AddOrUpdate()
            //    context.SaveChanges();
            //}
            //modelBuilder.Entity<Format>().HasData(
            //    new Format
            //    {
            //        Id = 1,
            //        Name = "Paperback"
            //    },
            //    new Format
            //    {
            //        Id = 2,
            //        Name = "Trade Paperback"
            //    },
            //    new Format
            //    {
            //        Id = 3,
            //        Name = "Hardcover"
            //    },
            //    new Format
            //    {
            //        Id = 4,
            //        Name = "Softcover"
            //    },
            //    new Format
            //    {
            //        Id = 5,
            //        Name = "Mass Market Paperback"
            //    },
            //    new Format
            //    {
            //        Id = 6,
            //        Name = "Map"
            //    },
            //    new Format
            //    {
            //        Id = 7,
            //        Name = "Kindle"
            //    },
            //    new Format
            //    {
            //        Id = 8,
            //        Name = "Leather Bound"
            //    }
            //);

            //modelBuilder.Entity<Genre>().HasData(
            //    new Genre
            //    {
            //        Id = 1,
            //        Name = "Science Fiction"
            //    },
            //    new Genre
            //    {
            //        Id = 2,
            //        Name = "Fantasy"
            //    }
            //);

            //modelBuilder.Entity<Author>().HasData(
            //    new Author
            //    {
            //        Id = 1,
            //        Name = "Douglas Adams"
            //    },
            //    new Author
            //    {
            //        Id = 2,
            //        Name = "Terry Pratchett"
            //    }
            //);

            //modelBuilder.Entity<Publisher>().HasData(
            //    new Publisher
            //    {
            //        Id = 1,
            //        Name = "Random House Digital, Inc"
            //    },
            //    new Publisher
            //    {
            //        Id = 2,
            //        Name = "HarperTorch"
            //    }
            //);

            //modelBuilder.Entity<Book>().HasData(
            //    new Book
            //    {
            //        Id = 1,
            //        Title = "Equal Rites",
            //        NumberOfPages = 213,
            //        PublisherId = 1,
            //        GenreId = 1,
            //        FormatId = 1,
            //        PublicationDate = DateTime.Now
            //        //ISBN = 9780061020698
            //    }
            //);
        }
    }
}
