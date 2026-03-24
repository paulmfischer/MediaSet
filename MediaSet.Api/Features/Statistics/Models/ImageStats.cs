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

/// <summary>
/// An entity for which background image lookup was attempted but failed.
/// </summary>
public record ImageLookupFailure(
    string EntityId,
    string EntityType,
    string Title,
    DateTime LookupAttemptedAt,
    string? FailureReason,
    bool PermanentFailure
);

public record ImageStats(
    int TotalFiles,
    long TotalSizeBytes,
    Dictionary<string, int> FilesByEntityType,
    Dictionary<string, long> SizeByEntityType,
    IReadOnlyList<BrokenImageLink> BrokenLinks,
    IReadOnlyList<OrphanedImageFile> OrphanedFiles,
    IReadOnlyList<ImageLookupFailure> ImageLookupFailures,
    DateTime LastUpdated
);
