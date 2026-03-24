using MediaSet.Api.Infrastructure.Caching;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Storage;
using MediaSet.Api.Shared.Models;
using Serilog;
using SerilogTracing;

namespace MediaSet.Api.Features.Images.Services;

public class ImageManagementService : IImageManagementService
{
    private const string ImageStatsCacheKey = "image-stats";

    private readonly IImageStorageProvider storageProvider;
    private readonly IEntityService<Book> bookService;
    private readonly IEntityService<Movie> movieService;
    private readonly IEntityService<Game> gameService;
    private readonly IEntityService<Music> musicService;
    private readonly ICacheService cacheService;
    private readonly ILogger<ImageManagementService> logger;

    public ImageManagementService(
        IImageStorageProvider _storageProvider,
        IEntityService<Book> _bookService,
        IEntityService<Movie> _movieService,
        IEntityService<Game> _gameService,
        IEntityService<Music> _musicService,
        ICacheService _cacheService,
        ILogger<ImageManagementService> _logger)
    {
        storageProvider = _storageProvider;
        bookService = _bookService;
        movieService = _movieService;
        gameService = _gameService;
        musicService = _musicService;
        cacheService = _cacheService;
        logger = _logger;
    }

    public async Task<int> DeleteOrphanedImagesAsync(CancellationToken cancellationToken = default)
    {
        using var activity = Log.Logger.StartActivity("DeleteOrphanedImages");

        var allFiles = storageProvider.ListFiles().ToList();

        var bookTask = bookService.GetListAsync(cancellationToken);
        var movieTask = movieService.GetListAsync(cancellationToken);
        var gameTask = gameService.GetListAsync(cancellationToken);
        var musicTask = musicService.GetListAsync(cancellationToken);
        await Task.WhenAll(bookTask, movieTask, gameTask, musicTask);

        var referencedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allEntities = bookTask.Result.Cast<IEntity>()
            .Concat(movieTask.Result.Cast<IEntity>())
            .Concat(gameTask.Result.Cast<IEntity>())
            .Concat(musicTask.Result.Cast<IEntity>());

        foreach (var entity in allEntities)
        {
            if (entity.CoverImage != null && !string.IsNullOrEmpty(entity.CoverImage.FilePath))
            {
                referencedPaths.Add(entity.CoverImage.FilePath);
            }
        }

        var orphanedFiles = allFiles
            .Where(f => !referencedPaths.Contains(f.RelativePath))
            .ToList();

        foreach (var (relativePath, _) in orphanedFiles)
        {
            logger.LogInformation("Deleting orphaned image file: {FilePath}", relativePath);
            storageProvider.DeleteImage(relativePath);
        }

        await cacheService.RemoveAsync(ImageStatsCacheKey);

        logger.LogInformation("Deleted {Count} orphaned image files and invalidated image stats cache", orphanedFiles.Count);

        return orphanedFiles.Count;
    }

    public async Task<int> ResetImageLookupAsync(IEnumerable<string> entityIds, string entityType, CancellationToken cancellationToken = default)
    {
        using var activity = Log.Logger.StartActivity("ResetImageLookup");

        var idSet = new HashSet<string>(entityIds, StringComparer.OrdinalIgnoreCase);
        var count = 0;

        switch (entityType.ToLowerInvariant())
        {
            case "books":
                var books = (await bookService.GetListAsync(cancellationToken))
                    .Where(e => e.Id != null && idSet.Contains(e.Id));
                foreach (var book in books)
                {
                    book.ImageLookup = null;
                    await bookService.UpdateAsync(book.Id!, book, cancellationToken);
                    count++;
                }
                break;
            case "movies":
                var movies = (await movieService.GetListAsync(cancellationToken))
                    .Where(e => e.Id != null && idSet.Contains(e.Id));
                foreach (var movie in movies)
                {
                    movie.ImageLookup = null;
                    await movieService.UpdateAsync(movie.Id!, movie, cancellationToken);
                    count++;
                }
                break;
            case "games":
                var games = (await gameService.GetListAsync(cancellationToken))
                    .Where(e => e.Id != null && idSet.Contains(e.Id));
                foreach (var game in games)
                {
                    game.ImageLookup = null;
                    await gameService.UpdateAsync(game.Id!, game, cancellationToken);
                    count++;
                }
                break;
            case "musics":
                var musics = (await musicService.GetListAsync(cancellationToken))
                    .Where(e => e.Id != null && idSet.Contains(e.Id));
                foreach (var music in musics)
                {
                    music.ImageLookup = null;
                    await musicService.UpdateAsync(music.Id!, music, cancellationToken);
                    count++;
                }
                break;
            default:
                throw new ArgumentException($"Unknown entity type: {entityType}");
        }

        await cacheService.RemoveAsync(ImageStatsCacheKey);

        logger.LogInformation("Reset ImageLookup for {Count} entities of type {EntityType}", count, entityType);

        return count;
    }
}
