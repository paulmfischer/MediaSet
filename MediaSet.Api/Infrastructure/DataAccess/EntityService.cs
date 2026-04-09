using MediaSet.Api.Shared.Models;

using System.Reflection;
using System.Text.RegularExpressions;
using MediaSet.Api.Infrastructure.Database;
using MediaSet.Api.Infrastructure.Caching;
using MongoDB.Bson;
using MongoDB.Driver;
using Serilog;
using SerilogTracing;

namespace MediaSet.Api.Infrastructure.DataAccess;

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

    public async Task<IEnumerable<TEntity>> SearchAsync(string searchText, string orderBy, CancellationToken cancellationToken = default)
    {
        using var activity = Log.Logger.StartActivity("Search {EntityType}", new { EntityType = entityTypeName, searchText, orderBy });

        string orderByField = nameof(IEntity.Title);
        bool orderByAscending = true;

        if (!string.IsNullOrWhiteSpace(orderBy))
        {
            var orderByArgs = orderBy.Split(":");
            var requestedField = orderByArgs[0];

            if (!string.IsNullOrWhiteSpace(requestedField))
            {
                var property = typeof(TEntity).GetProperty(requestedField, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                if (property == null)
                {
                    throw new ArgumentException($"Invalid order by field '{requestedField}' for entity type '{entityTypeName}'.");
                }
                orderByField = property.Name;
            }

            if (orderByArgs.Length >= 2 && !string.IsNullOrWhiteSpace(orderByArgs[1]))
            {
                orderByAscending = orderByArgs[1].ToLower() == "asc";
            }
        }

        var searchPattern = Regex.Escape(searchText);
        var filter = Builders<TEntity>.Filter.Regex(entity => entity.Title, new BsonRegularExpression(searchPattern, "i"));
        var sort = orderByAscending
            ? Builders<TEntity>.Sort.Ascending(orderByField)
            : Builders<TEntity>.Sort.Descending(orderByField);

        var findOptions = new FindOptions<TEntity> { Sort = sort };
        using var cursor = await entityCollection.FindAsync(filter, findOptions, cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<TEntity>> PagedSearchAsync(string searchText, string orderBy, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        using var activity = Log.Logger.StartActivity("PagedSearch {EntityType}", new { EntityType = entityTypeName, searchText, orderBy, page, pageSize });

        string orderByField = nameof(IEntity.Title);
        bool orderByAscending = true;

        if (!string.IsNullOrWhiteSpace(orderBy))
        {
            var orderByArgs = orderBy.Split(":");
            var requestedField = orderByArgs[0];

            if (!string.IsNullOrWhiteSpace(requestedField))
            {
                var property = typeof(TEntity).GetProperty(requestedField, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                if (property == null)
                {
                    throw new ArgumentException($"Invalid order by field '{requestedField}' for entity type '{entityTypeName}'.");
                }
                orderByField = property.Name;
            }

            if (orderByArgs.Length >= 2 && !string.IsNullOrWhiteSpace(orderByArgs[1]))
            {
                orderByAscending = orderByArgs[1].ToLower() == "asc";
            }
        }

        var searchPattern = Regex.Escape(searchText);
        var filter = Builders<TEntity>.Filter.Regex(entity => entity.Title, new BsonRegularExpression(searchPattern, "i"));
        var sort = orderByAscending
            ? Builders<TEntity>.Sort.Ascending(orderByField)
            : Builders<TEntity>.Sort.Descending(orderByField);

        var totalCount = await entityCollection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        var findOptions = new FindOptions<TEntity>
        {
            Sort = sort,
            Skip = (page - 1) * pageSize,
            Limit = pageSize
        };

        using var cursor = await entityCollection.FindAsync(filter, findOptions, cancellationToken);
        var items = await cursor.ToListAsync(cancellationToken);

        return new PagedResult<TEntity>(items, totalCount, page, pageSize);
    }

    public async Task<IEnumerable<TEntity>> GetListAsync(CancellationToken cancellationToken = default)
    {
        using var activity = Log.Logger.StartActivity("GetList {EntityType}", new { EntityType = entityTypeName });
        
        var findOptions = new FindOptions<TEntity>
        {
            Sort = Builders<TEntity>.Sort.Ascending(entity => entity.Title)
        };
        using var cursor = await entityCollection.FindAsync(_ => true, findOptions, cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }

    public async Task<TEntity?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        using var activity = Log.Logger.StartActivity("Get {EntityType}", new { EntityType = entityTypeName, id });
        return await entityCollection.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateAsync(TEntity newEntity, CancellationToken cancellationToken = default)
    {
        using var activity = Log.Logger.StartActivity("Create {EntityType}", new { EntityType = entityTypeName, entityId = newEntity.Id });
        
        await entityCollection.InsertOneAsync(newEntity, null, cancellationToken);
        await InvalidateCachesAsync();
    }

    public async Task<ReplaceOneResult> UpdateAsync(string id, TEntity updatedEntity, CancellationToken cancellationToken = default)
    {
        using var activity = Log.Logger.StartActivity("Update {EntityType}", new { EntityType = entityTypeName, id });
        
        var result = await entityCollection.ReplaceOneAsync(x => x.Id == id, updatedEntity, cancellationToken: cancellationToken);
        await InvalidateCachesAsync();
        return result;
    }

    public async Task<DeleteResult> RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        using var activity = Log.Logger.StartActivity("Remove {EntityType}", new { EntityType = entityTypeName, id });
        
        var result = await entityCollection.DeleteOneAsync(x => x.Id == id, cancellationToken);
        await InvalidateCachesAsync();
        return result;
    }

    public async Task BulkCreateAsync(IEnumerable<TEntity> newEntities, CancellationToken cancellationToken = default)
    {
        using var activity = Log.Logger.StartActivity("BulkCreate {EntityType}", new { EntityType = entityTypeName, count = newEntities.Count() });
        
        await entityCollection.InsertManyAsync(newEntities, null, cancellationToken);
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
