namespace MediaSet.Api.Upload;

public interface IUploadService<TEntity>
{
  IEnumerable<TEntity> MapUploadToEntities(IList<string> headerFields, IList<string[]> dataFields);
}