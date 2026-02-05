using MediaSet.Api.Features.Entities.Models;
using MediaSet.Api.Models;

namespace MediaSet.Api.Services;

public interface IMetadataService
{
    Task<IEnumerable<string>> GetMetadata(MediaTypes mediaType, string propertyName, CancellationToken cancellationToken = default);
}
