using MediaSet.Api.Models;

namespace MediaSet.Api.Services;

public interface IMetadataService
{
    Task<IEnumerable<string>> GetFormats(MediaTypes mediaType);
    Task<IEnumerable<string>> GetGenres(MediaTypes mediaType);
    Task<IEnumerable<string>> GetMetadata(MediaTypes mediaType, string propertyName);
}
