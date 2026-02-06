using MediaSet.Api.Features.Entities.Models;
using MediaSet.Api.Infrastructure.Lookup.Models;

namespace MediaSet.Api.Features.Metadata.Services;

public interface IMetadataService
{
    Task<IEnumerable<string>> GetMetadata(MediaTypes mediaType, string propertyName, CancellationToken cancellationToken = default);
}
