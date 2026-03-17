namespace MediaSet.Api.Features.Statistics.Models;

/// <summary>
/// An entity whose CoverImage.FilePath points to a file that doesn't exist on disk.
/// </summary>
public record BrokenImageLink(
    string EntityId,
    string EntityType,
    string Title,
    string MissingFilePath
);

/// <summary>
/// A file on disk not referenced by any entity's CoverImage.FilePath.
/// </summary>
public record OrphanedImageFile(
    string RelativePath,
    long SizeBytes
);

public record ImageStats(
    int TotalFiles,
    long TotalSizeBytes,
    Dictionary<string, int> FilesByEntityType,
    Dictionary<string, long> SizeByEntityType,
    IReadOnlyList<BrokenImageLink> BrokenLinks,
    IReadOnlyList<OrphanedImageFile> OrphanedFiles,
    DateTime LastUpdated
);
