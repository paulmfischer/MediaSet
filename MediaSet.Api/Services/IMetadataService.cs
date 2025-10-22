using MediaSet.Api.Models;

namespace MediaSet.Api.Services;

public interface IMetadataService
{
    Task<IEnumerable<string>> GetMetadata(MediaTypes mediaType, string propertyName);
}
