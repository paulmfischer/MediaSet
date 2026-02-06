using MediaSet.Api.Features.Entities.Models;
using System.Reflection;
using MediaSet.Api.Shared.Extensions;
using Serilog;
using SerilogTracing;

namespace MediaSet.Api.Features.Entities.Services;

public class UploadService
{
    public static IEnumerable<TEntity> MapUploadToEntities<TEntity>(IList<string> headerFields, IList<string[]> dataFields) where TEntity : IEntity, new()
    {
        using var activity = Log.Logger.StartActivity("MapUploadToEntities {EntityType}", new { EntityType = typeof(TEntity).Name, rowCount = dataFields.Count });
        
        IList<TEntity> entities = new List<TEntity>(dataFields.Count);

        foreach (string[] dataRow in dataFields)
        {
            var newEntity = new TEntity();
            entities.Add(newEntity);
            foreach (var property in newEntity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property != null)
                {
                    var value = dataRow.GetValueByHeader<Movie>(headerFields, property);
                    if (value != null)
                    {
                        property.SetValue(newEntity, value.CastTo(property));
                    }
                }
            }
        }

        return entities;
    }
}
