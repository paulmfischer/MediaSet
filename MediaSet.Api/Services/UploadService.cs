using System.Reflection;
using MediaSet.Api.Helpers;
using MediaSet.Api.Models;

namespace MediaSet.Api.Services;

public class UploadService
{
    public static IEnumerable<TEntity> MapUploadToEntities<TEntity>(IList<string> headerFields, IList<string[]> dataFields) where TEntity : IEntity, new()
    {
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
              property.SetValue(newEntity, value.CastTo(property.PropertyType));
            }
          }
        }
      }

      return entities;
    }
}