using System.Reflection;
using MediaSet.Api.Attributes;
using MediaSet.Api.Clients;
using MediaSet.Api.Models;

namespace MediaSet.Api.Services;

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

        // Step 2: Find the lookup identifier property
        var identifierValue = GetLookupIdentifierValue(entity);
        if (string.IsNullOrWhiteSpace(identifierValue))
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

        // Step 3: Determine the identifier type based on media type
        var identifierType = GetIdentifierType(mediaType, identifierValue);

        // Step 4: Perform external API lookup
        _logger.LogInformation(
            "Looking up image for entity {EntityId} with {IdentifierType}: {IdentifierValue}",
            entity.Id, identifierType, identifierValue);

        try
        {
            var imageUrl = await LookupImageUrlAsync(mediaType, identifierType, identifierValue, cancellationToken);

            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                _logger.LogInformation(
                    "No image URL found for entity {EntityId} with {IdentifierType}: {IdentifierValue}",
                    entity.Id, identifierType, identifierValue);

                return new ImageLookupResult(
                    Success: false,
                    ImageUrl: null,
                    SavedImage: null,
                    ErrorMessage: "No image URL returned from lookup");
            }

            // Step 5: Download and save the image
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

    private string? GetLookupIdentifierValue(IEntity entity)
    {
        var entityType = entity.GetType();
        var properties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            var lookupIdentifierAttr = property.GetCustomAttribute<LookupIdentifierAttribute>();
            if (lookupIdentifierAttr != null)
            {
                var value = property.GetValue(entity);
                return value?.ToString();
            }
        }

        return null;
    }

    private static IdentifierType GetIdentifierType(MediaTypes mediaType, string identifierValue)
    {
        // Books use ISBN, others use UPC/EAN (barcode)
        if (mediaType == MediaTypes.Books)
        {
            return IdentifierType.Isbn;
        }

        // For Movies, Games, Music - determine if it's UPC (12 digits) or EAN (13 digits)
        return identifierValue.Length == 13 ? IdentifierType.Ean : IdentifierType.Upc;
    }

    private async Task<string?> LookupImageUrlAsync(
        MediaTypes mediaType,
        IdentifierType identifierType,
        string identifierValue,
        CancellationToken cancellationToken)
    {
        object? response = mediaType switch
        {
            MediaTypes.Books => await _strategyFactory
                .GetStrategy<BookResponse>(mediaType, identifierType)
                .LookupAsync(identifierType, identifierValue, cancellationToken),
            MediaTypes.Movies => await _strategyFactory
                .GetStrategy<MovieResponse>(mediaType, identifierType)
                .LookupAsync(identifierType, identifierValue, cancellationToken),
            MediaTypes.Games => await _strategyFactory
                .GetStrategy<GameResponse>(mediaType, identifierType)
                .LookupAsync(identifierType, identifierValue, cancellationToken),
            MediaTypes.Musics => await _strategyFactory
                .GetStrategy<MusicResponse>(mediaType, identifierType)
                .LookupAsync(identifierType, identifierValue, cancellationToken),
            _ => null
        };

        return response switch
        {
            BookResponse book => book.ImageUrl,
            MovieResponse movie => movie.ImageUrl,
            GameResponse game => game.ImageUrl,
            MusicResponse music => music.ImageUrl,
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
