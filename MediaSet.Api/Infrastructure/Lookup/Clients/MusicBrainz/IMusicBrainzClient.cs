using MediaSet.Api.Infrastructure.Lookup.Models;

namespace MediaSet.Api.Infrastructure.Lookup.Clients.MusicBrainz;

public interface IMusicBrainzClient
{
    Task<MusicBrainzRelease?> GetReleaseByBarcodeAsync(string barcode, CancellationToken cancellationToken);
    /// <summary>All releases MusicBrainz lists for a barcode (up to 10), so a caller can tell whether the barcode is ambiguous.</summary>
    Task<IReadOnlyList<MusicBrainzRelease>> SearchByBarcodeAsync(string barcode, CancellationToken cancellationToken);
    Task<MusicBrainzRelease?> GetReleaseByIdAsync(string releaseId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MusicBrainzRelease>> SearchByEntityPropertiesAsync(IReadOnlyDictionary<string, string> searchParams, CancellationToken cancellationToken);
}
