using MediaSet.Schema.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace MediaSet.Schema
{
    public interface IMediaSetDbContext
    {
        DbSet<Book> Books { get; set; }
        DbSet<Author> Authors { get; set; }
        DbSet<Format> Formats { get; set; }
        DbSet<Genre> Genres { get; set; }
        DbSet<Publisher> Publishers { get; set; }

        int SaveChanges();
        EntityEntry<TEntity> Entry<TEntity>([NotNull] TEntity entity) where TEntity : class;
        EntityEntry<TEntity> Remove<TEntity>([NotNull] TEntity entity) where TEntity : class;
    }
}
