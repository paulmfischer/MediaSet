using System.Reflection;
using MediaSet.Api.Infrastructure.Lookup;
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
        cancellationToken.ThrowIfCancellationRequested();
        if (entity.CoverImage != null || entity.ImageLookup?.LastOutcome == ImageLookupOutcome.Success)
        {
            return new ImageLookupResult(true, null, entity.CoverImage, null, Outcome: ImageLookupOutcome.Success);
        }
        ImageLookupResult? existingFailure = null;

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

            existingFailure = existingUrlResult;

            _logger.LogWarning(
                "Failed to download from existing ImageUrl for entity {EntityId}: {Error}",
                entity.Id, existingUrlResult.ErrorMessage);
        }

        // Step 2: Find the lookup identifier — tries properties in Order, uses first with a value
        var lookupIdentifier = GetLookupIdentifier(entity);
        if (lookupIdentifier == null)
        {
            if (ImageRetryPolicy.IsRetryable(existingFailure?.Outcome))
            {
                return existingFailure!;
            }
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
        var strategy = $"{mediaType}:{identifierType}";
        var provider = mediaType switch
        {
            MediaTypes.Books => "OpenLibrary",
            MediaTypes.Movies => "Tmdb",
            MediaTypes.Games => "Igdb",
            MediaTypes.Musics => "MusicBrainz",
            _ => "Unknown"
        };

        // Step 3: Perform external API lookup
        _logger.LogInformation(
            "Looking up image for entity {EntityId} with {IdentifierType}: {IdentifierValue}",
            entity.Id, identifierType, identifierValue);

        try
        {
            using var failures = new MediaSet.Api.Infrastructure.Lookup.LookupFailureScope();
            var (matched, imageUrl) = await LookupImageUrlAsync(mediaType, identifierType, identifierValue, cancellationToken);
            provider = failures.AttemptedProviders ?? provider;
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                failures.ThrowIfFailed();
                if (ImageRetryPolicy.IsRetryable(existingFailure?.Outcome))
                {
                    return existingFailure!;
                }
                _logger.LogWarning(
                    "No image URL found for entity {EntityId} with {IdentifierType}: {IdentifierValue}",
                    entity.Id, identifierType, identifierValue);

                return new ImageLookupResult(
                    Success: false,
                    ImageUrl: null,
                    SavedImage: null,
                    ErrorMessage: matched ? "Matched entity has no artwork" : "No image URL: no matching entity",
                    PermanentFailure: true,
                    Outcome: matched ? ImageLookupOutcome.MissingArtwork : ImageLookupOutcome.NoMatch,
                    Provider: provider, AttemptedStrategy: strategy);
            }

            // Step 4: Download and save the image
            var downloaded = await TryDownloadImageAsync(imageUrl, mediaType.ToString(), entity.Id, cancellationToken);
            return downloaded with { Provider = provider, AttemptedStrategy = strategy };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ProviderLookupException ex)
        {
            // A terminal fallback result cannot invalidate the known URL's retryable download failure.
            if (!ImageRetryPolicy.IsRetryable(ex.Outcome) && ImageRetryPolicy.IsRetryable(existingFailure?.Outcome))
            {
                return existingFailure!;
            }
            return new ImageLookupResult(false, null, null, ex.Message, Outcome: ex.Outcome,
                PermanentFailure: !ImageRetryPolicy.IsRetryable(ex.Outcome),
                Provider: ex.Outcome == ImageLookupOutcome.AmbiguousMatch ? provider : ex.Provider,
                AttemptedStrategy: strategy, RetryAt: ex.RetryAt);
        }
        catch (NotSupportedException ex)
        {
            if (ImageRetryPolicy.IsRetryable(existingFailure?.Outcome))
            {
                return existingFailure!;
            }
            _logger.LogWarning(ex,
                "No lookup strategy available for {MediaType} with {IdentifierType}",
                mediaType, identifierType);

            return new ImageLookupResult(
                Success: false,
                ImageUrl: null,
                SavedImage: null,
                ErrorMessage: $"No lookup strategy available: {ex.Message}",
                PermanentFailure: true, Outcome: ImageLookupOutcome.UnsupportedStrategy);
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
                ErrorMessage: $"Lookup error: {ex.Message}", Outcome: ImageLookupOutcome.ProviderUnavailable,
                Provider: provider, AttemptedStrategy: strategy);
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

    private async Task<(bool Matched, string? ImageUrl)> LookupImageUrlAsync(
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
            System.Collections.ICollection { Count: > 1 } => throw new ProviderLookupException(
                mediaType.ToString(), ImageLookupOutcome.AmbiguousMatch, "Multiple artwork candidates require a confident match"),
            IReadOnlyList<BookResponse> books => (books.Count > 0, books.FirstOrDefault()?.ImageUrl),
            IReadOnlyList<MovieResponse> movies => (movies.Count > 0, movies.FirstOrDefault()?.ImageUrl),
            IReadOnlyList<GameResponse> games => (games.Count > 0, games.FirstOrDefault()?.ImageUrl),
            IReadOnlyList<MusicResponse> music => (music.Count > 0, music.FirstOrDefault()?.ImageUrl),
            _ => (false, null)
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
                ErrorMessage: null, Provider: "ImageDownload", AttemptedStrategy: "DownloadUrl");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to download image from {ImageUrl} for entity {EntityId}",
                imageUrl, entityId);

            var cause = ex;
            while (cause.InnerException != null)
            {
                cause = cause.InnerException;
            }
            var missing = cause is HttpRequestException { StatusCode: System.Net.HttpStatusCode.NotFound };
            var invalid = ex is ArgumentException && cause is not HttpRequestException;
            return new ImageLookupResult(
                Success: false,
                ImageUrl: imageUrl,
                SavedImage: null,
                ErrorMessage: $"Download failed: {ex.Message}",
                PermanentFailure: missing || invalid,
                Outcome: missing ? ImageLookupOutcome.MissingArtwork : invalid ? ImageLookupOutcome.InvalidArtwork
                    : ImageLookupOutcome.DownloadFailure,
                Provider: "ImageDownload", AttemptedStrategy: "DownloadUrl");
        }
    }
}
