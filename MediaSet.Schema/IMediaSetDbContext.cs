using MediaSet.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Diagnostics.CodeAnalysis;

namespace MediaSet.Data
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
