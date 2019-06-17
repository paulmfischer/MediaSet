using MediaSet.Schema.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MediaSet.Schema
{
    public partial class MediaSetDbContext : DbContext, IMediaSetDbContext
    {
        private readonly IConfiguration config;

        public DbSet<Book> Books { get; set; }
        public DbSet<Author> Authors { get; set; }
        public DbSet<Format> Formats { get; set; }
        public DbSet<Genre> Genres { get; set; }
        public DbSet<Publisher> Publishers { get; set; }

        public MediaSetDbContext(IConfiguration config) : base()
        {
            this.config = config;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(config.GetConnectionString("DefaultConnection"));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.SetupBookAuthorMapping();

            //modelBuilder.Seed();
        }
    }
}
