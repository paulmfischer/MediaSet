using MediaSet.Api.Features.Entities.Models;
using MediaSet.Api.Features.Lookup.Models;
using System.Reflection;
using MediaSet.Api.Infrastructure.Caching;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Features.Entities.Models;
using Microsoft.Extensions.Options;
using Serilog;
using SerilogTracing;

namespace MediaSet.Api.Features.Metadata.Services;

public class MetadataService : IMetadataService
{
    private readonly IServiceProvider serviceProvider;
    private readonly ICacheService cacheService;
    private readonly CacheSettings cacheSettings;
    private readonly ILogger<MetadataService> logger;

    public MetadataService(
        IServiceProvider _serviceProvider,
        ICacheService _cacheService,
        IOptions<CacheSettings> _cacheSettings,
        ILogger<MetadataService> _logger)
    {
        serviceProvider = _serviceProvider;
        cacheService = _cacheService;
        cacheSettings = _cacheSettings.Value;
        logger = _logger;
    }

    public async Task<IEnumerable<string>> GetMetadata(MediaTypes mediaType, string propertyName, CancellationToken cancellationToken = default)
    {
        using var activity = Log.Logger.StartActivity("GetMetadata {MediaType}", new { MediaType = mediaType, propertyName });
        
        var cacheKey = $"metadata:{mediaType}:{propertyName}";
        
        // Try to get from cache
        var cachedResult = await cacheService.GetAsync<List<string>>(cacheKey);
        if (cachedResult != null)
        {
            logger.LogDebug("Returning cached metadata for {mediaType}:{propertyName}", mediaType, propertyName);
            return cachedResult;
        }

        logger.LogDebug("Cache miss for metadata {mediaType}:{propertyName}, fetching from database", mediaType, propertyName);

        var entityType = GetEntityType(mediaType);
        var serviceType = typeof(IEntityService<>).MakeGenericType(entityType);
        var service = serviceProvider.GetService(serviceType);

        if (service == null)
        {
            throw new InvalidOperationException($"Service not found for {entityType.Name}");
        }

        var getListMethod = serviceType.GetMethod(nameof(IEntityService<IEntity>.GetListAsync));
        if (getListMethod == null)
        {
            throw new InvalidOperationException($"GetListAsync method not found on {serviceType.Name}");
        }

        var task = (Task)getListMethod.Invoke(service, new object[] { cancellationToken })!;
        await task.ConfigureAwait(false);

        var resultProperty = task.GetType().GetProperty("Result");
        var entities = (System.Collections.IEnumerable)resultProperty!.GetValue(task)!;

        var property = entityType.GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (property == null)
        {
            throw new ArgumentException($"Property {propertyName} not found on {entityType.Name}");
        }

        var results = new List<string>();

        foreach (var entity in entities)
        {
            var value = property.GetValue(entity);
            
            if (value is string stringValue && !string.IsNullOrWhiteSpace(stringValue))
            {
                results.Add(stringValue.Trim());
            }
            else if (value is IEnumerable<string> listValue)
            {
                results.AddRange(listValue.Select(v => v.Trim()));
            }
        }

        var distinctResults = results.Distinct().Order().ToList();
        
        // Cache the results
        await cacheService.SetAsync(cacheKey, distinctResults);
        logger.LogInformation("Cached metadata for {mediaType}:{propertyName} with {count} distinct values", mediaType, propertyName, distinctResults.Count);

        return distinctResults;
    }

    private static Type GetEntityType(MediaTypes mediaType) => mediaType switch
    {
        MediaTypes.Books => typeof(Book),
        MediaTypes.Movies => typeof(Movie),
        MediaTypes.Games => typeof(Game),
        MediaTypes.Musics => typeof(Music),
        _ => throw new ArgumentException($"Unknown media type: {mediaType}")
    };
}
