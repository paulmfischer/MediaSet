using System.Reflection;
using MediaSet.Api.Helpers;
using MediaSet.Api.Models;

namespace MediaSet.Api.Upload;

public class UploadService
{
    public IEnumerable<TEntity> MapUploadToEntities<TEntity>(IList<string> headerFields, IList<string[]> dataFields) where TEntity : IEntity, new()
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
              var propertyType = property.PropertyType;
              if (propertyType == typeof(List<string>))
              {
                IList<string> values = [.. value.Split("|").Select(val => val.Trim())];
                property.SetValue(newEntity, values);
              }
              else if (propertyType == typeof(int?))
              {
                property.SetValue(newEntity, int.Parse(value));
              }
              else
              {
                property.SetValue(newEntity, value);
              }
            }
          }
        }
      }

      return entities;
    }
}