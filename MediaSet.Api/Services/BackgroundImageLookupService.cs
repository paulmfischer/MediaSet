using Cronos;
using MediaSet.Api.Clients;
using MediaSet.Api.Infrastructure.Database;
using MediaSet.Api.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace MediaSet.Api.Services;

/// <summary>
/// Background service that automatically finds and downloads cover images for entities missing them.
/// </summary>
public class BackgroundImageLookupService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IOptions<BackgroundImageLookupConfiguration> _config;
    private readonly ILogger<BackgroundImageLookupService> _logger;

    public BackgroundImageLookupService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<BackgroundImageLookupConfiguration> config,
        ILogger<BackgroundImageLookupService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var scopeProperties = new Dictionary<string, object?>
        {
            ["Application"] = "MediaSet.Api.Processor.BackgroundImage"
        };

        using (_logger.BeginScope(scopeProperties))
        {
            _logger.LogInformation("Background image lookup service started");

            var cronExpression = CronExpression.Parse(_config.Value.Schedule);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var localTimeZone = TimeZoneInfo.Local; // Respects TZ environment variable
                    var now = DateTime.UtcNow;
                    var nextRun = cronExpression.GetNextOccurrence(now, localTimeZone);

                    if (nextRun == null)
                    {
                        _logger.LogWarning("No next occurrence found for cron expression, stopping service");
                        break;
                    }

                    var delay = nextRun.Value - now;
                    _logger.LogInformation(
                        "Next background image lookup run scheduled for {NextRun} {TimeZone} (in {Delay})",
                        nextRun.Value, localTimeZone.Id, delay);

                    await Task.Delay(delay, stoppingToken);

                    if (!stoppingToken.IsCancellationRequested)
                    {
                        await ProcessAllEntityTypesAsync(stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Expected when stopping
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in background image lookup service main loop");
                    // Wait a bit before retrying to avoid rapid failure loops
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }

            _logger.LogInformation("Background image lookup service stopped");
        }
    }

    private async Task ProcessAllEntityTypesAsync(CancellationToken cancellationToken)
    {
        var runStartTime = DateTime.UtcNow;
        var maxRuntime = TimeSpan.FromMinutes(_config.Value.MaxRuntimeMinutes);
        var totalProcessed = 0;
        var totalSucceeded = 0;
        var totalFailed = 0;

        _logger.LogInformation(
            "Starting background image lookup run. BatchSize: {BatchSize}, MaxRuntime: {MaxRuntime} minutes",
            _config.Value.BatchSize, _config.Value.MaxRuntimeMinutes);

        using var scope = _serviceScopeFactory.CreateScope();
        var strategyFactory = scope.ServiceProvider.GetRequiredService<LookupStrategyFactory>();

        // Determine which entity types have available strategies
        var availableMediaTypes = GetAvailableMediaTypes(strategyFactory);

        if (availableMediaTypes.Count == 0)
        {
            _logger.LogWarning("No lookup strategies available, skipping run");
            return;
        }

        _logger.LogInformation(
            "Processing {Count} media types: {MediaTypes}",
            availableMediaTypes.Count, string.Join(", ", availableMediaTypes));

        // Calculate per-type batch limit (proportional allocation)
        var perTypeLimit = Math.Max(1, _config.Value.BatchSize / availableMediaTypes.Count);
        var remainder = _config.Value.BatchSize % availableMediaTypes.Count;

        foreach (var mediaType in availableMediaTypes)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            // Check max runtime
            if (DateTime.UtcNow - runStartTime > maxRuntime)
            {
                _logger.LogInformation(
                    "Max runtime of {MaxRuntime} minutes reached, stopping processing",
                    _config.Value.MaxRuntimeMinutes);
                break;
            }

            // Give extra slot to first types if there's a remainder
            var limit = perTypeLimit + (remainder-- > 0 ? 1 : 0);

            var (processed, succeeded, failed) = await ProcessMediaTypeAsync(
                scope, mediaType, limit, runStartTime, maxRuntime, cancellationToken);

            totalProcessed += processed;
            totalSucceeded += succeeded;
            totalFailed += failed;
        }

        _logger.LogInformation(
            "Background image lookup run completed. Total processed: {Processed}, Succeeded: {Succeeded}, Failed: {Failed}, Duration: {Duration}",
            totalProcessed, totalSucceeded, totalFailed, DateTime.UtcNow - runStartTime);
    }

    private List<MediaTypes> GetAvailableMediaTypes(LookupStrategyFactory strategyFactory)
    {
        var availableTypes = new List<MediaTypes>();

        // Check each media type to see if a strategy is available
        foreach (MediaTypes mediaType in Enum.GetValues<MediaTypes>())
        {
            try
            {
                // Try to get a strategy - if it succeeds, the type is available
                // Books use ISBN, others use UPC/EAN
                var identifierType = mediaType == MediaTypes.Books
                    ? IdentifierType.Isbn
                    : IdentifierType.Upc;

                // We need to try getting the strategy to see if it's registered
                // The factory throws NotSupportedException if not available
                _ = mediaType switch
                {
                    MediaTypes.Books => (object)strategyFactory.GetStrategy<BookResponse>(mediaType, identifierType),
                    MediaTypes.Movies => strategyFactory.GetStrategy<MovieResponse>(mediaType, identifierType),
                    MediaTypes.Games => strategyFactory.GetStrategy<GameResponse>(mediaType, identifierType),
                    MediaTypes.Musics => strategyFactory.GetStrategy<MusicResponse>(mediaType, identifierType),
                    _ => throw new NotSupportedException()
                };

                availableTypes.Add(mediaType);
            }
            catch (NotSupportedException)
            {
                _logger.LogDebug("No lookup strategy available for {MediaType}", mediaType);
            }
        }

        return availableTypes;
    }

    private async Task<(int processed, int succeeded, int failed)> ProcessMediaTypeAsync(
        IServiceScope scope,
        MediaTypes mediaType,
        int limit,
        DateTime runStartTime,
        TimeSpan maxRuntime,
        CancellationToken cancellationToken)
    {
        var processed = 0;
        var succeeded = 0;
        var failed = 0;

        try
        {
            var (entities, collection) = await GetEntitiesWithoutImagesAsync(scope, mediaType, limit, cancellationToken);

            _logger.LogInformation(
                "Found {Count} {MediaType} entities without images (limit: {Limit})",
                entities.Count, mediaType, limit);

            foreach (var entity in entities)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                // Check max runtime
                if (DateTime.UtcNow - runStartTime > maxRuntime)
                {
                    _logger.LogInformation("Max runtime reached during {MediaType} processing", mediaType);
                    break;
                }

                var success = await ProcessEntityAsync(scope, entity, mediaType, collection, cancellationToken);
                processed++;

                if (success)
                    succeeded++;
                else
                    failed++;

                // Apply rate limiting
                await ApplyRateLimitAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing {MediaType} entities", mediaType);
        }

        return (processed, succeeded, failed);
    }

    private async Task<(List<IEntity> entities, object collection)> GetEntitiesWithoutImagesAsync(
        IServiceScope scope,
        MediaTypes mediaType,
        int limit,
        CancellationToken cancellationToken)
    {
        var databaseService = scope.ServiceProvider.GetRequiredService<IDatabaseService>();

        return mediaType switch
        {
            MediaTypes.Books => await GetEntitiesAsync<Book>(databaseService, limit, cancellationToken),
            MediaTypes.Movies => await GetEntitiesAsync<Movie>(databaseService, limit, cancellationToken),
            MediaTypes.Games => await GetEntitiesAsync<Game>(databaseService, limit, cancellationToken),
            MediaTypes.Musics => await GetEntitiesAsync<Music>(databaseService, limit, cancellationToken),
            _ => (new List<IEntity>(), null!)
        };
    }

    private async Task<(List<IEntity> entities, IMongoCollection<TEntity> collection)> GetEntitiesAsync<TEntity>(
        IDatabaseService databaseService,
        int limit,
        CancellationToken cancellationToken) where TEntity : IEntity
    {
        var collection = databaseService.GetCollection<TEntity>();

        // Query for entities where CoverImage is null and ImageLookup is null
        var filter = Builders<TEntity>.Filter.And(
            Builders<TEntity>.Filter.Eq(e => e.CoverImage, null),
            Builders<TEntity>.Filter.Eq(e => e.ImageLookup, null)
        );

        var entities = await collection
            .Find(filter)
            .Limit(limit)
            .ToListAsync(cancellationToken);

        return (entities.Cast<IEntity>().ToList(), collection);
    }

    private async Task<bool> ProcessEntityAsync(
        IServiceScope scope,
        IEntity entity,
        MediaTypes mediaType,
        object collection,
        CancellationToken cancellationToken)
    {
        try
        {
            var imageLookupService = scope.ServiceProvider.GetRequiredService<IImageLookupService>();
            var result = await imageLookupService.LookupAndSaveImageAsync(entity, mediaType, cancellationToken);

            // Update the entity with the result
            await UpdateEntityAsync(entity, mediaType, result, collection, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation(
                    "Successfully processed image for {MediaType} entity {EntityId}",
                    mediaType, entity.Id);
            }
            else
            {
                _logger.LogInformation(
                    "Failed to find image for {MediaType} entity {EntityId}: {Error}",
                    mediaType, entity.Id, result.ErrorMessage);
            }

            return result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing {MediaType} entity {EntityId}",
                mediaType, entity.Id);

            // Still update the entity with failure info
            try
            {
                var failureResult = new ImageLookupResult(
                    Success: false,
                    ImageUrl: null,
                    SavedImage: null,
                    ErrorMessage: $"Processing error: {ex.Message}");

                await UpdateEntityAsync(entity, mediaType, failureResult, collection, cancellationToken);
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx,
                    "Failed to update {MediaType} entity {EntityId} with failure info",
                    mediaType, entity.Id);
            }

            return false;
        }
    }

    private async Task UpdateEntityAsync(
        IEntity entity,
        MediaTypes mediaType,
        ImageLookupResult result,
        object collection,
        CancellationToken cancellationToken)
    {
        var imageLookup = new Models.ImageLookup
        {
            LookupAttemptedAt = DateTime.UtcNow,
            FailureReason = result.Success ? null : result.ErrorMessage,
            PermanentFailure = result.PermanentFailure
        };

        switch (mediaType)
        {
            case MediaTypes.Books:
                await UpdateEntityInCollectionAsync<Book>((IMongoCollection<Book>)collection, entity.Id!, imageLookup, result.SavedImage, cancellationToken);
                break;
            case MediaTypes.Movies:
                await UpdateEntityInCollectionAsync<Movie>((IMongoCollection<Movie>)collection, entity.Id!, imageLookup, result.SavedImage, cancellationToken);
                break;
            case MediaTypes.Games:
                await UpdateEntityInCollectionAsync<Game>((IMongoCollection<Game>)collection, entity.Id!, imageLookup, result.SavedImage, cancellationToken);
                break;
            case MediaTypes.Musics:
                await UpdateEntityInCollectionAsync<Music>((IMongoCollection<Music>)collection, entity.Id!, imageLookup, result.SavedImage, cancellationToken);
                break;
        }
    }

    private static async Task UpdateEntityInCollectionAsync<TEntity>(
        IMongoCollection<TEntity> collection,
        string entityId,
        Models.ImageLookup imageLookup,
        Image? coverImage,
        CancellationToken cancellationToken) where TEntity : IEntity
    {
        var filter = Builders<TEntity>.Filter.Eq(e => e.Id, entityId);
        var updateBuilder = Builders<TEntity>.Update.Set(e => e.ImageLookup, imageLookup);

        // Also update CoverImage if it was successfully retrieved
        if (coverImage != null)
        {
            updateBuilder = updateBuilder.Set(e => e.CoverImage, coverImage);
        }

        await collection.UpdateOneAsync(filter, updateBuilder, cancellationToken: cancellationToken);
    }

    private async Task ApplyRateLimitAsync(CancellationToken cancellationToken)
    {
        var delayMs = (int)(60_000.0 / _config.Value.RequestsPerMinute);
        await Task.Delay(delayMs, cancellationToken);
    }
}
