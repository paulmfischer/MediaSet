using System.Reflection;
using MediaSet.Api.Models;

namespace MediaSet.Api.Services;

public class MetadataService : IMetadataService
{
    private readonly IServiceProvider serviceProvider;

    public MetadataService(IServiceProvider _serviceProvider)
    {
        serviceProvider = _serviceProvider;
    }

    public Task<IEnumerable<string>> GetFormats(MediaTypes mediaType) => 
        GetMetadata(mediaType, nameof(IEntity.Format));

    public Task<IEnumerable<string>> GetGenres(MediaTypes mediaType) => 
        GetMetadata(mediaType, "Genres");

    public async Task<IEnumerable<string>> GetMetadata(MediaTypes mediaType, string propertyName)
    {
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

        var task = (Task)getListMethod.Invoke(service, null)!;
        await task.ConfigureAwait(false);

        var resultProperty = task.GetType().GetProperty("Result");
        var entities = (System.Collections.IEnumerable)resultProperty!.GetValue(task)!;

        var property = entityType.GetProperty(propertyName);
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

        return results.Distinct().Order();
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
