using System.Reflection;
using MediaSet.Api.Shared.Models;
using MediaSet.Api.Infrastructure.Lookup.Models;
using MediaSet.Api.Infrastructure.Lookup.Strategies;
using MediaSet.Api.Infrastructure.Storage;
using MediaSet.Api.Shared.Attributes;
using MediaSet.Api.Shared.Extensions;

namespace MediaSet.Api.Features.Images.Services;

/// <summary>
/// Service that orchestrates image lookup for entities using lookup strategies.
/// </summary>
public class ImageLookupService : IImageLookupService
{
    private readonly LookupStrategyFactory _strategyFactory;
    private readonly IImageService _imageService;
    private readonly ILogger<ImageLookupService> _logger;

    public ImageLookupService(
        LookupStrategyFactory strategyFactory,
        IImageService imageService,
        ILogger<ImageLookupService> logger)
    {
        _strategyFactory = strategyFactory;
        _imageService = imageService;
        _logger = logger;
    }

    public async Task<ImageLookupResult> LookupAndSaveImageAsync(
        IEntity entity,
        MediaTypes mediaType,
        CancellationToken cancellationToken)
    {
        // Check if entity already has an Id (required for saving image)
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            return new ImageLookupResult(
                Success: false,
                ImageUrl: null,
                SavedImage: null,
                ErrorMessage: "Entity does not have an Id",
                PermanentFailure: true);
        }

        // Step 1: If entity has existing ImageUrl, try downloading from it first
        if (!string.IsNullOrWhiteSpace(entity.ImageUrl))
        {
            _logger.LogInformation(
                "Entity {EntityId} has existing ImageUrl, attempting download from {ImageUrl}",
                entity.Id, entity.ImageUrl);

            var existingUrlResult = await TryDownloadImageAsync(
                entity.ImageUrl,
                mediaType.ToString(),
                entity.Id,
                cancellationToken);

            if (existingUrlResult.Success)
            {
                return existingUrlResult;
            }

            _logger.LogWarning(
                "Failed to download from existing ImageUrl for entity {EntityId}: {Error}",
                entity.Id, existingUrlResult.ErrorMessage);
        }

        // Step 2: Find the lookup identifier — tries properties in Order, uses first with a value
        var lookupIdentifier = GetLookupIdentifier(entity);
        if (lookupIdentifier == null)
        {
            _logger.LogWarning(
                "Entity {EntityId} of type {MediaType} has no lookup identifier value",
                entity.Id, mediaType);

            return new ImageLookupResult(
                Success: false,
                ImageUrl: null,
                SavedImage: null,
                ErrorMessage: "Entity lacks required lookup identifier",
                PermanentFailure: true);
        }

        var (identifierType, identifierValue) = lookupIdentifier.Value;

        // Step 3: Perform external API lookup
        _logger.LogInformation(
            "Looking up image for entity {EntityId} with {IdentifierType}: {IdentifierValue}",
            entity.Id, identifierType, identifierValue);

        try
        {
            var imageUrl = await LookupImageUrlAsync(mediaType, identifierType, identifierValue, cancellationToken);

            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                _logger.LogWarning(
                    "No image URL found for entity {EntityId} with {IdentifierType}: {IdentifierValue}",
                    entity.Id, identifierType, identifierValue);

                return new ImageLookupResult(
                    Success: false,
                    ImageUrl: null,
                    SavedImage: null,
                    ErrorMessage: "No image URL returned from lookup");
            }

            // Step 4: Download and save the image
            return await TryDownloadImageAsync(imageUrl, mediaType.ToString(), entity.Id, cancellationToken);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex,
                "No lookup strategy available for {MediaType} with {IdentifierType}",
                mediaType, identifierType);

            return new ImageLookupResult(
                Success: false,
                ImageUrl: null,
                SavedImage: null,
                ErrorMessage: $"No lookup strategy available: {ex.Message}",
                PermanentFailure: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error during image lookup for entity {EntityId}",
                entity.Id);

            return new ImageLookupResult(
                Success: false,
                ImageUrl: null,
                SavedImage: null,
                ErrorMessage: $"Lookup error: {ex.Message}");
        }
    }

    /// <summary>
    /// Finds all properties marked with <see cref="LookupIdentifierAttribute"/>, sorts them by
    /// <see cref="LookupIdentifierAttribute.Order"/>, and returns the identifier type and value
    /// from the first property that has a non-empty value. Returns null if no property has a value.
    /// </summary>
    private static (IdentifierType IdentifierType, string Value)? GetLookupIdentifier(IEntity entity)
    {
        var properties = entity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var candidates = properties
            .Select(p => (Prop: p, Attr: p.GetCustomAttribute<LookupIdentifierAttribute>()))
            .Where(x => x.Attr != null)
            .OrderBy(x => x.Attr!.Order);

        foreach (var (prop, attr) in candidates)
        {
            var value = prop.GetValue(entity)?.ToString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return (attr!.IdentifierType, value);
            }
        }

        return null;
    }

    private async Task<string?> LookupImageUrlAsync(
        MediaTypes mediaType,
        IdentifierType identifierType,
        string identifierValue,
        CancellationToken cancellationToken)
    {
        var searchParams = identifierType == IdentifierType.Entity
            ? new Dictionary<string, string> { ["title"] = identifierValue }
            : new Dictionary<string, string> { [identifierType.ToApiString()] = identifierValue };

        object? response = mediaType switch
        {
            MediaTypes.Books => await _strategyFactory
                .GetStrategy<BookResponse>(mediaType, identifierType)
                .LookupAsync(identifierType, searchParams, cancellationToken),
            MediaTypes.Movies => await _strategyFactory
                .GetStrategy<MovieResponse>(mediaType, identifierType)
                .LookupAsync(identifierType, searchParams, cancellationToken),
            MediaTypes.Games => await _strategyFactory
                .GetStrategy<GameResponse>(mediaType, identifierType)
                .LookupAsync(identifierType, searchParams, cancellationToken),
            MediaTypes.Musics => await _strategyFactory
                .GetStrategy<MusicResponse>(mediaType, identifierType)
                .LookupAsync(identifierType, searchParams, cancellationToken),
            _ => null
        };

        return response switch
        {
            IReadOnlyList<BookResponse> books => books.FirstOrDefault()?.ImageUrl,
            IReadOnlyList<MovieResponse> movies => movies.FirstOrDefault()?.ImageUrl,
            IReadOnlyList<GameResponse> games => games.FirstOrDefault()?.ImageUrl,
            IReadOnlyList<MusicResponse> music => music.FirstOrDefault()?.ImageUrl,
            _ => null
        };
    }

    private async Task<ImageLookupResult> TryDownloadImageAsync(
        string imageUrl,
        string entityType,
        string entityId,
        CancellationToken cancellationToken)
    {
        try
        {
            var image = await _imageService.DownloadAndSaveImageAsync(
                imageUrl,
                entityType,
                entityId,
                cancellationToken);

            _logger.LogInformation(
                "Successfully downloaded and saved image for entity {EntityId} from {ImageUrl}",
                entityId, imageUrl);

            return new ImageLookupResult(
                Success: true,
                ImageUrl: imageUrl,
                SavedImage: image,
                ErrorMessage: null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to download image from {ImageUrl} for entity {EntityId}",
                imageUrl, entityId);

            return new ImageLookupResult(
                Success: false,
                ImageUrl: imageUrl,
                SavedImage: null,
                ErrorMessage: $"Download failed: {ex.Message}");
        }
    }
}
