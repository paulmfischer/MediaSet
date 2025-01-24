using System.Reflection;
using MediaSet.Api.Helpers;
using MediaSet.Api.Models;

namespace MediaSet.Api.Upload;

public class MovieUploadService : IUploadService<Movie>
{
    public IEnumerable<Movie> MapUploadToEntities(IList<string> headerFields, IList<string[]> dataFields)
    {
      IList<Movie> movies = new List<Movie>(dataFields.Count);
      
      foreach (string[] dataRow in dataFields)
      {
        var newMovie = new Movie();
        movies.Add(newMovie);
        foreach (var property in newMovie.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
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
                property.SetValue(newMovie, values);
              }
              else if (propertyType == typeof(int?))
              {
                property.SetValue(newMovie, int.Parse(value));
              }
              else
              {
                property.SetValue(newMovie, value);
              }
            }
          }
        }
      }

      return movies;
    }
}