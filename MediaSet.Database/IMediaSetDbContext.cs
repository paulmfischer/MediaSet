using MediaSet.Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace MediaSet.Data
{
    public interface IMediaSetDbContext
    {
        DbSet<Book> Books { get; set; }
        DbSet<Author> Authors { get; set; }
        DbSet<Format> Formats { get; set; }
        DbSet<Genre> Genres { get; set; }
        DbSet<Publisher> Publishers { get; set; }
    }
}
