﻿using Microsoft.EntityFrameworkCore;
using MediaSet.Data.MovieData;
using MediaSet.Data.BookData;
using Microsoft.Extensions.Logging;

namespace MediaSet.Data
{
    public class MediaSetContext : DbContext, IMediaSetContext
    {
        private bool attachLogging { get; set; }
        public MediaSetContext(bool attachLogging = false)
        {
            this.attachLogging = attachLogging;
        }

        public MediaSetContext() { }

        public DbSet<Media> Media { get; set; }
        public DbSet<MediaType> MediaTypes { get; set; }
        public DbSet<Genre> Genres { get; set; }
        public DbSet<Format> Formats { get; set; }
        public DbSet<Studio> Studios { get; set; }
        public DbSet<Movie> Movies { get; set; }
        public DbSet<Book> Books { get; set; }
        public DbSet<Author> Authors { get; set; }
        public DbSet<Publisher> Publishers { get; set; }
        public DbSet<Director> Directors { get; set; }
        public DbSet<Producer> Producers { get; set; }
        public DbSet<Writer> Writers { get; set; }

        public static readonly ILoggerFactory MyLoggerFactory
            = LoggerFactory.Create(builder => { builder.AddConsole(); });

        //protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=MediaSet;Trusted_Connection=True;MultipleActiveResultSets=true");
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options
                .UseSqlServer(@"Data Source=.\SQLEXPRESS;Initial Catalog=MediaSet;Integrated Security=SSPI");

            if (attachLogging)
            {
                options.UseLoggerFactory(MyLoggerFactory);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.SetupMediaGenreMapping();
            modelBuilder.SetupMovieMappings();
            modelBuilder.SetupBookAuthorMapping();

            modelBuilder.Seed();
        }
    }
}
