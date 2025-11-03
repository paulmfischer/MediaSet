using MediaSet.Api.Models;

namespace MediaSet.Api.Clients;

public interface IMusicBrainzClient
{
    Task<MusicBrainzRelease?> GetReleaseByBarcodeAsync(string barcode, CancellationToken cancellationToken);
    Task<MusicBrainzRelease?> GetReleaseByIdAsync(string releaseId, CancellationToken cancellationToken);
}
