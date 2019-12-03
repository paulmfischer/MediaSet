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
        //public DbSet<Genre> Genres { get; set; }
        //public DbSet<Format> Formats { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseSqlite("Data Source=MediaSet.db");
    }
}
