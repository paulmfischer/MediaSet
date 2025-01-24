using MediaSet.Api.Models;

namespace MediaSet.Api.Upload;

public static class UploadFactoryService
{
  public static IUploadService<TEntity> GetUploadService<TEntity>() where TEntity : IEntity
  {
    var entityType = typeof(TEntity);
    if (entityType == typeof(Movie)) {
      return (IUploadService<TEntity>)new MovieUploadService();
    }
    else if (entityType == typeof(Book)) {
      return (IUploadService<TEntity>)new MovieUploadService();
    }
    
    throw new ArgumentException($"No UploadService provided for {typeof(TEntity)}");
  }
}