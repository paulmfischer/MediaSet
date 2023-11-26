using MediaSet.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MediaSet.Data.Repositories;

public interface IMetadataRepository
{
    Task<List<TEntity>> GetAll<TEntity>() where TEntity : class, IMetadata;
    Task<TEntity?> GetById<TEntity>(int id) where TEntity : class, IMetadata;
    Task<TEntity> Create<TEntity>(TEntity entity) where TEntity : class, IMetadata;
    Task<TEntity?> Update<TEntity>(TEntity entity) where TEntity : class, IMetadata;
    Task<int> DeleteById<TEntity>(int id) where TEntity : class, IMetadata;
}

public class MetadataRepository : IMetadataRepository
{
    private readonly MediaSetDbContext db;

    public MetadataRepository(MediaSetDbContext context)
    {
        db = context;
    }

    public async Task<TEntity> Create<TEntity>(TEntity entity) where TEntity : class, IMetadata
    {
        db.GetDbSet<TEntity>().Add(entity);
        await db.SaveChangesAsync();

        return entity;
    }

    public Task<int> DeleteById<TEntity>(int id) where TEntity : class, IMetadata
    {
        return db.GetDbSet<TEntity>().Where(entity => entity.Id == id).ExecuteDeleteAsync();
    }

    public Task<List<TEntity>> GetAll<TEntity>() where TEntity : class, IMetadata
    {
        return db.GetDbSet<TEntity>().AsNoTracking().ToListAsync();
    }

    public Task<TEntity?> GetById<TEntity>(int id) where TEntity : class, IMetadata
    {
        return db.GetDbSet<TEntity>().FirstOrDefaultAsync(entity => entity.Id == id);
    }

    public async Task<TEntity?> Update<TEntity>(TEntity entity) where TEntity : class, IMetadata
    {
        var rowsUpdated = await db.GetDbSet<TEntity>().Where(f => f.Id == entity.Id)
                .ExecuteUpdateAsync(updates =>
                    updates.SetProperty(f => f.Name, entity.Name)
                );
        return rowsUpdated == 0 ? null : entity;
    }
}

internal static class MediaSetDbContextExtensions
{
    public static DbSet<TEntity> GetDbSet<TEntity>(this MediaSetDbContext db) where TEntity : class
    {
        Type entityType = typeof(TEntity);

        if (entityType == typeof(Format))
        {
            return db.Formats as DbSet<TEntity>;
        }
        else if (entityType == typeof(Genre))
        {
            return db.Genres as DbSet<TEntity>;
        }
        else //if (entityType == typeof(Publisher))
        {
            return db.Publishers as DbSet<TEntity>;
        }
    }
}