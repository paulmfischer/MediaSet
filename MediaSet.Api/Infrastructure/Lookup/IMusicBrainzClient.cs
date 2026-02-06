using MediaSet.Api.Features.Lookup.Models;
using MediaSet.Api.Features.Entities.Models;

namespace MediaSet.Api.Infrastructure.Lookup;

public interface IMusicBrainzClient
{
    Task<MusicBrainzRelease?> GetReleaseByBarcodeAsync(string barcode, CancellationToken cancellationToken);
    Task<MusicBrainzRelease?> GetReleaseByIdAsync(string releaseId, CancellationToken cancellationToken);
}
