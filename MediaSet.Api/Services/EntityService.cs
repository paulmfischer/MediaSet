using System.Linq.Expressions;
using MediaSet.Api.Models;
using MongoDB.Driver;

namespace MediaSet.Api.Services;

public class EntityService<TEntity> : IEntityService<TEntity> where TEntity : IEntity
{
    private readonly IMongoCollection<TEntity> entityCollection;
    private readonly ICacheService cacheService;
    private readonly ILogger<EntityService<TEntity>> logger;
    private readonly string entityTypeName;

    public EntityService(
        IDatabaseService databaseService,
        ICacheService _cacheService,
        ILogger<EntityService<TEntity>> _logger)
    {
        entityCollection = databaseService.GetCollection<TEntity>();
        cacheService = _cacheService;
        logger = _logger;
        entityTypeName = typeof(TEntity).Name;
    }

    public async Task<IEnumerable<TEntity>> SearchAsync(string searchText, string orderBy)
    {
        string orderByField = "";
        bool orderByAscending = true;
        if (!string.IsNullOrWhiteSpace(orderBy))
        {
            var orderByArgs = orderBy.Split(":");
            orderByField = orderByArgs[0];
            orderByAscending = string.IsNullOrWhiteSpace(orderByArgs[1]) || orderByArgs[1].ToLower().Equals("asc");
        }
        var entitySearch = entityCollection.Find(entity => entity.Title.ToLower().Contains(searchText.ToLower()));
        Expression<Func<TEntity, object>> sortFn = entity => entity.Title;

        if (orderByAscending)
        {
            entitySearch.SortBy(sortFn);
        }
        else
        {
            entitySearch.SortByDescending(sortFn);
        }
        return await entitySearch.ToListAsync();
    }

    public async Task<IEnumerable<TEntity>> GetListAsync() => await entityCollection.Find(_ => true).SortBy(entity => entity.Title).ToListAsync();

    public async Task<TEntity?> GetAsync(string id) => await entityCollection.Find(x => x.Id == id).FirstOrDefaultAsync();

    public async Task CreateAsync(TEntity newEntity)
    {
        await entityCollection.InsertOneAsync(newEntity);
        await InvalidateCachesAsync();
    }

    public async Task<ReplaceOneResult> UpdateAsync(string id, TEntity updatedEntity)
    {
        var result = await entityCollection.ReplaceOneAsync(x => x.Id == id, updatedEntity);
        await InvalidateCachesAsync();
        return result;
    }

    public async Task<DeleteResult> RemoveAsync(string id)
    {
        var result = await entityCollection.DeleteOneAsync(x => x.Id == id);
        await InvalidateCachesAsync();
        return result;
    }

    public async Task BulkCreateAsync(IEnumerable<TEntity> newEntities)
    {
        await entityCollection.InsertManyAsync(newEntities);
        await InvalidateCachesAsync();
    }

    /// <summary>
    /// Invalidates all caches related to this entity type.
    /// This includes metadata caches for this entity type and the global stats cache.
    /// </summary>
    private async Task InvalidateCachesAsync()
    {
        // Determine the media type for cache key pattern
        var mediaType = GetMediaType();
        
        // Invalidate all metadata caches for this entity type
        var metadataPattern = $"metadata:{mediaType}:*";
        await cacheService.RemoveByPatternAsync(metadataPattern);
        
        // Invalidate stats cache
        await cacheService.RemoveAsync("stats");
        
        logger.LogInformation("Invalidated caches for entity type: {entityType}", entityTypeName);
    }

    /// <summary>
    /// Maps entity type to MediaTypes enum value for cache key construction.
    /// </summary>
    private MediaTypes GetMediaType() => entityTypeName switch
    {
        nameof(Book) => MediaTypes.Books,
        nameof(Movie) => MediaTypes.Movies,
        nameof(Game) => MediaTypes.Games,
        nameof(Music) => MediaTypes.Musics,
        _ => throw new InvalidOperationException($"Unknown entity type: {entityTypeName}")
    };
}
