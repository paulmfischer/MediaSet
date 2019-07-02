using MediaSet.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Configuration;
using System.IO;
using System.Linq;

namespace MediaSet.Data
{
    public partial class MediaSetDbContext : DbContext, IMediaSetDbContext
    {
        private readonly IConfiguration config;

        public DbSet<Book> Books { get; set; }
        public DbSet<Author> Authors { get; set; }
        public DbSet<Format> Formats { get; set; }
        public DbSet<Genre> Genres { get; set; }
        public DbSet<Publisher> Publishers { get; set; }
        public DbSet<BookAuthor> BookAuthor { get; set; }
        public DbSet<Media> Media { get; set; }
        //public DbSet<PersonalInformation> PersonalInformation { get; set; }

        public MediaSetDbContext(IConfiguration config) : base()
        {
            this.config = config;
        }

        public MediaSetDbContext() : base() { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(config.GetConnectionString("DefaultConnection"));

            // uncomment when running migrations, fix config DI later
            //optionsBuilder.UseSqlServer(@"Data Source=.\SQLEXPRESS;Initial Catalog=MediaSet;Integrated Security=SSPI");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.SetupBookAuthorMapping();
            modelBuilder.SetupMediaGenreMapping();

            //modelBuilder.Seed();
        }

        //public override int SaveChanges()
        //{
        //    var newEntries = ChangeTracker.Entries().Where(x => x.State == EntityState.Added);

        //    foreach(var item in newEntries)
        //    {
        //        item.Property("AddedDateTime").CurrentValue = DateTime.UtcNow;
        //    }

        //    var modifiedEntries = ChangeTracker.Entries().Where(x => x.State == EntityState.Modified);

        //    foreach (var item in modifiedEntries)
        //    {
        //        item.Property("UpdatedDateTime").CurrentValue = DateTime.UtcNow;
        //    }

        //    return base.SaveChanges();
        //}
    }
}
