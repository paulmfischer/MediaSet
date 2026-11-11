using MediaSet.Api.Features.Statistics.Models;
using MediaSet.Api.Infrastructure.Caching;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Storage;
using MediaSet.Api.Shared.Models;
using Microsoft.Extensions.Options;
using Serilog;
using SerilogTracing;

namespace MediaSet.Api.Features.Statistics.Services;

public class ImageStatsService : IImageStatsService
{
    private const string CacheKey = "image-stats";

    private readonly IImageStorageProvider _storageProvider;
    private readonly IEntityService<Book> _bookService;
    private readonly IEntityService<Movie> _movieService;
    private readonly IEntityService<Game> _gameService;
    private readonly IEntityService<Music> _musicService;
    private readonly ICacheService _cacheService;
    private readonly CacheSettings _cacheSettings;
    private readonly ILogger<ImageStatsService> _logger;

    public ImageStatsService(
        IImageStorageProvider _storageProvider,
        IEntityService<Book> _bookService,
        IEntityService<Movie> _movieService,
        IEntityService<Game> _gameService,
        IEntityService<Music> _musicService,
        ICacheService _cacheService,
        IOptions<CacheSettings> _cacheSettings,
        ILogger<ImageStatsService> _logger)
    {
        this._storageProvider = _storageProvider;
        this._bookService = _bookService;
        this._movieService = _movieService;
        this._gameService = _gameService;
        this._musicService = _musicService;
        this._cacheService = _cacheService;
        this._cacheSettings = _cacheSettings.Value;
        this._logger = _logger;
    }

    public async Task<ImageStats?> GetImageStatsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = Log.Logger.StartActivity("GetImageStats");

        var cached = await _cacheService.GetAsync<ImageStats>(CacheKey);
        if (cached != null)
        {
            _logger.LogDebug("Returning cached image statistics");
            return cached;
        }

        _logger.LogDebug("Cache miss for image statistics, computing");

        // List all files in storage
        var allFiles = _storageProvider.ListFiles().ToList();

        // Group by first path segment (entity type directory)
        var filesByEntityType = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var sizeByEntityType = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        foreach (var (relativePath, sizeBytes) in allFiles)
        {
            var firstSegment = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
            filesByEntityType.TryGetValue(firstSegment, out var count);
            filesByEntityType[firstSegment] = count + 1;
            sizeByEntityType.TryGetValue(firstSegment, out var size);
            sizeByEntityType[firstSegment] = size + sizeBytes;
        }

        // Load all entities in parallel
        var bookTask = _bookService.GetListAsync(cancellationToken);
        var movieTask = _movieService.GetListAsync(cancellationToken);
        var gameTask = _gameService.GetListAsync(cancellationToken);
        var musicTask = _musicService.GetListAsync(cancellationToken);
        await Task.WhenAll(bookTask, movieTask, gameTask, musicTask);

        // Build entity type label map
        var entityGroups = new[]
        {
            (Entities: bookTask.Result.Cast<IEntity>(), TypeLabel: "books"),
            (Entities: movieTask.Result.Cast<IEntity>(), TypeLabel: "movies"),
            (Entities: gameTask.Result.Cast<IEntity>(), TypeLabel: "games"),
            (Entities: musicTask.Result.Cast<IEntity>(), TypeLabel: "musics"),
        };

        // Compute broken links, lookup failures, and collect all referenced file paths
        var referencedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var brokenLinks = new List<BrokenImageLink>();
        var imageLookupFailures = new List<ImageLookupFailure>();

        foreach (var (entities, typeLabel) in entityGroups)
        {
            foreach (var entity in entities)
            {
                if (entity.ImageLookup != null &&
                    (!string.IsNullOrEmpty(entity.ImageLookup.FailureReason) || entity.ImageLookup.PermanentFailure))
                {
                    imageLookupFailures.Add(new ImageLookupFailure(
                        EntityId: entity.Id!,
                        EntityType: typeLabel,
                        Title: entity.Title,
                        LookupAttemptedAt: entity.ImageLookup.LookupAttemptedAt,
                        FailureReason: entity.ImageLookup.FailureReason,
                        PermanentFailure: entity.ImageLookup.PermanentFailure,
                        Provider: entity.ImageLookup.Provider,
                        Outcome: entity.ImageLookup.LastOutcome?.ToString(),
                        AttemptedStrategy: entity.ImageLookup.AttemptedStrategy,
                        NextEligibleAt: entity.ImageLookup.NextEligibleAt,
                        AttemptCount: entity.ImageLookup.AttemptCount
                    ));
                }

                if (entity.CoverImage == null || string.IsNullOrEmpty(entity.CoverImage.FilePath))
                {
                    continue;
                }

                referencedPaths.Add(entity.CoverImage.FilePath);

                if (!_storageProvider.Exists(entity.CoverImage.FilePath))
                {
                    brokenLinks.Add(new BrokenImageLink(
                        EntityId: entity.Id!,
                        EntityType: typeLabel,
                        Title: entity.Title,
                        MissingFilePath: entity.CoverImage.FilePath
                    ));
                }
            }
        }

        // Compute orphaned files: on disk but not referenced by any entity
        var orphanedFiles = allFiles
            .Where(f => !referencedPaths.Contains(f.RelativePath))
            .Select(f => new OrphanedImageFile(f.RelativePath, f.SizeBytes))
            .ToList();

        var stats = new ImageStats(
            TotalFiles: allFiles.Count,
            TotalSizeBytes: allFiles.Sum(f => f.SizeBytes),
            FilesByEntityType: filesByEntityType,
            SizeByEntityType: sizeByEntityType,
            BrokenLinks: brokenLinks,
            OrphanedFiles: orphanedFiles,
            ImageLookupFailures: imageLookupFailures,
            LastUpdated: DateTime.UtcNow
        );

        await _cacheService.SetAsync(CacheKey, stats,
            TimeSpan.FromMinutes(_cacheSettings.StatsCacheDurationMinutes));

        _logger.LogInformation(
            "Image statistics computed: {TotalFiles} files, {TotalSizeBytes} bytes, {OrphanedFiles} orphaned, {BrokenLinks} broken links, {LookupFailures} lookup failures",
            stats.TotalFiles, stats.TotalSizeBytes, stats.OrphanedFiles.Count, stats.BrokenLinks.Count, stats.ImageLookupFailures.Count);

        return stats;
    }
}
