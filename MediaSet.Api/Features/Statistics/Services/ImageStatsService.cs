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

    private readonly IImageStorageProvider storageProvider;
    private readonly IEntityService<Book> bookService;
    private readonly IEntityService<Movie> movieService;
    private readonly IEntityService<Game> gameService;
    private readonly IEntityService<Music> musicService;
    private readonly ICacheService cacheService;
    private readonly CacheSettings cacheSettings;
    private readonly ILogger<ImageStatsService> logger;

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
        storageProvider = _storageProvider;
        bookService = _bookService;
        movieService = _movieService;
        gameService = _gameService;
        musicService = _musicService;
        cacheService = _cacheService;
        cacheSettings = _cacheSettings.Value;
        logger = _logger;
    }

    public async Task<ImageStats?> GetImageStatsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = Log.Logger.StartActivity("GetImageStats");

        var cached = await cacheService.GetAsync<ImageStats>(CacheKey);
        if (cached != null)
        {
            logger.LogDebug("Returning cached image statistics");
            return cached;
        }

        logger.LogDebug("Cache miss for image statistics, computing");

        // List all files in storage
        var allFiles = storageProvider.ListFiles().ToList();

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
        var bookTask = bookService.GetListAsync(cancellationToken);
        var movieTask = movieService.GetListAsync(cancellationToken);
        var gameTask = gameService.GetListAsync(cancellationToken);
        var musicTask = musicService.GetListAsync(cancellationToken);
        await Task.WhenAll(bookTask, movieTask, gameTask, musicTask);

        // Build entity type label map
        var entityGroups = new[]
        {
            (Entities: bookTask.Result.Cast<IEntity>(), TypeLabel: "books"),
            (Entities: movieTask.Result.Cast<IEntity>(), TypeLabel: "movies"),
            (Entities: gameTask.Result.Cast<IEntity>(), TypeLabel: "games"),
            (Entities: musicTask.Result.Cast<IEntity>(), TypeLabel: "musics"),
        };

        // Compute broken links and collect all referenced file paths
        var referencedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var brokenLinks = new List<BrokenImageLink>();

        foreach (var (entities, typeLabel) in entityGroups)
        {
            foreach (var entity in entities)
            {
                if (entity.CoverImage == null || string.IsNullOrEmpty(entity.CoverImage.FilePath))
                {
                    continue;
                }

                referencedPaths.Add(entity.CoverImage.FilePath);

                if (!storageProvider.Exists(entity.CoverImage.FilePath))
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
            LastUpdated: DateTime.UtcNow
        );

        await cacheService.SetAsync(CacheKey, stats,
            TimeSpan.FromMinutes(cacheSettings.StatsCacheDurationMinutes));

        logger.LogInformation(
            "Image statistics computed: {TotalFiles} files, {TotalSizeBytes} bytes, {OrphanedFiles} orphaned, {BrokenLinks} broken links",
            stats.TotalFiles, stats.TotalSizeBytes, stats.OrphanedFiles.Count, stats.BrokenLinks.Count);

        return stats;
    }
}
