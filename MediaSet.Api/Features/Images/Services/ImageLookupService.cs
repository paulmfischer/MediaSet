using System.Reflection;
using MediaSet.Api.Infrastructure.Lookup;
using MediaSet.Api.Infrastructure.Lookup.Models;
using MediaSet.Api.Infrastructure.Lookup.Strategies;
using MediaSet.Api.Infrastructure.Storage;
using MediaSet.Api.Shared.Attributes;
using MediaSet.Api.Shared.Extensions;
using MediaSet.Api.Shared.Models;

namespace MediaSet.Api.Features.Images.Services;

public class ImageLookupService(LookupStrategyFactory strategyFactory, IImageService imageService,
    ILogger<ImageLookupService> logger) : IImageLookupService
{
    public async Task<ImageLookupResult> LookupAndSaveImageAsync(IEntity entity, MediaTypes mediaType,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (entity.CoverImage != null || entity.ImageLookup?.LastOutcome == ImageLookupOutcome.Success)
        {
            return new(true, null, entity.CoverImage, null, Outcome: ImageLookupOutcome.Success);
        }
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            return new(false, null, null, "Entity does not have an Id", true, ImageLookupOutcome.MissingMetadata);
        }

        var fingerprint = ImageRetryPolicy.Fingerprint(entity);
        var confirmed = entity.ImageLookup?.ConfirmedMatch is { } previous && previous.MetadataFingerprint == fingerprint
            ? previous : null;
        ImageLookupResult? failure = null;
        var triedUrls = new HashSet<string>(StringComparer.Ordinal);

        void Remember(ImageLookupResult result)
        {
            confirmed = result.ConfirmedMatch ?? confirmed;
            if (ImageRetryPolicy.IsRetryable(result.Outcome)
                && (result.RetryAt > failure?.RetryAt || result.Outcome == ImageLookupOutcome.QuotaLimited))
            {
                failure = result;
                return;
            }
            // Preserve retryable errors across later terminal misses and preserve unresolved ambiguity.
            if ((failure == null || !ImageRetryPolicy.IsRetryable(failure.Outcome))
                && (failure?.Outcome != ImageLookupOutcome.AmbiguousMatch || ImageRetryPolicy.IsRetryable(result.Outcome))
                && (failure == null || result.Outcome != ImageLookupOutcome.UnsupportedStrategy))
            {
                failure = result;
            }
        }

        async Task<ImageLookupResult?> Download(string? url, string provider, string strategy)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var coverUri)
                && coverUri.Host.Equals("covers.openlibrary.org", StringComparison.OrdinalIgnoreCase)
                && coverUri.AbsolutePath.StartsWith("/b/", StringComparison.Ordinal))
            {
                url = new UriBuilder(coverUri) { Scheme = "https", Port = -1, Query = "default=false" }.Uri.AbsoluteUri;
            }
            if (string.IsNullOrWhiteSpace(url) || !triedUrls.Add(url))
            {
                return null;
            }
            var result = await TryDownloadImageAsync(url, mediaType.ToString(), entity.Id, cancellationToken);
            result = result with { Provider = provider, AttemptedStrategy = strategy, ConfirmedMatch = confirmed };
            if (!result.Success)
            {
                Remember(result);
            }
            return result;
        }

        var existing = await Download(entity.ImageUrl, "ImageDownload", "DownloadUrl");
        if (existing?.Success == true)
        {
            return existing;
        }
        var cached = await Download(confirmed?.ImageUrl, confirmed?.Provider ?? "Unknown", "ConfirmedMatch");
        if (cached?.Success == true)
        {
            return cached;
        }

        var attempts = entity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => (Property: p, Attribute: p.GetCustomAttribute<LookupIdentifierAttribute>()))
            .Where(p => p.Attribute != null && p.Attribute.IdentifierType != IdentifierType.Entity)
            .OrderBy(p => p.Attribute!.Order)
            .Select(p => (Type: p.Attribute!.IdentifierType, Value: p.Property.GetValue(entity)?.ToString()))
            .Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .Where(_ => confirmed == null)
            .Select(p => (p.Type, Parameters: new Dictionary<string, string> { [p.Type.ToApiString()] = p.Value! }))
            .ToList();
        if (confirmed != null && (mediaType != MediaTypes.Books
            || confirmed.ProviderId.StartsWith("OL", StringComparison.Ordinal) && confirmed.ProviderId.EndsWith('M')))
        {
            attempts.Add((mediaType == MediaTypes.Books ? IdentifierType.Olid : IdentifierType.Entity,
                new Dictionary<string, string>
                {
                    [mediaType == MediaTypes.Books ? "olid" : "confirmedProviderId"] = confirmed.ProviderId,
                    ["title"] = entity.Title
                }));
        }
        if (!string.IsNullOrWhiteSpace(entity.Title))
        {
            attempts.Add((IdentifierType.Entity, ImageCandidateMatcher.SearchContext(entity)));
        }
        if (mediaType == MediaTypes.Movies)
        {
            foreach (var attempt in attempts.Where(a => a.Type != IdentifierType.Entity))
            {
                attempt.Parameters["storedTitle"] = entity.Title;
            }
        }

        foreach (var (type, parameters) in attempts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var strategy = $"{mediaType}:{type}";
            try
            {
                using var failures = new LookupFailureScope();
                IEnumerable<ImageCandidate> candidates;
                try
                {
                    candidates = await LookupCandidatesAsync(mediaType, type, parameters, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    // Legacy clients may both record a typed failure and throw a generic HTTP error.
                    // Preserve the recorded quota classification and reset before disposing its scope.
                    failures.ThrowIfFailed();
                    throw;
                }
                cancellationToken.ThrowIfCancellationRequested();
                // An unavailable resource can hide an equally matching film, series, or remake.
                // Do not accept the surviving candidate from an incomplete provider search.
                if (mediaType == MediaTypes.Movies) { failures.ThrowIfFailed(); }
                var matching = ImageCandidateMatcher.Select(entity, candidates);
                foreach (var candidate in matching)
                {
                    if (candidate.Provenance is { } provenance)
                    {
                        confirmed = new(provenance.Provider, provenance.ProviderId, provenance.Title, candidate.ImageUrl,
                            fingerprint, "Full title and all available year, creator and platform constraints agree",
                            provenance.ArtworkScope ?? (entity is Game ? "Game artwork; region and package unverified" : "Provider artwork"));
                    }
                    var download = await Download(candidate.ImageUrl,
                        candidate.Provenance?.Provider ?? failures.AttemptedProviders ?? mediaType.ToString(), strategy);
                    if (download?.Success == true)
                    {
                        return download;
                    }
                }
                failures.ThrowIfFailed();
                // Identifier covers can exist even when the metadata endpoint has
                // no record. Downloading with default=false verifies availability.
                if (entity is Book && type is IdentifierType.Isbn or IdentifierType.Olid
                    && parameters.TryGetValue(type.ToApiString(), out var identifier)
                    && (type != IdentifierType.Olid || identifier.EndsWith('M')))
                {
                    var direct = await Download(
                        Infrastructure.Lookup.Clients.OpenLibrary.OpenLibraryClient.CoverUrl(type.ToApiString(), identifier),
                        "OpenLibrary", strategy);
                    if (direct?.Success == true)
                    {
                        return direct with { ConfirmedMatch = new("OpenLibrary", identifier, entity.Title,
                            direct.ImageUrl, fingerprint, "Cover resolved by stored edition identifier", "Exact-edition artwork") };
                    }
                }
                Remember(new(false, null, null, matching.Count > 0 ? "Matched entity has no usable artwork" : "No image URL: no matching entity",
                    true, matching.Count > 0 ? ImageLookupOutcome.MissingArtwork : ImageLookupOutcome.NoMatch,
                    failures.AttemptedProviders, strategy, ConfirmedMatch: confirmed));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (RetailBundleException ex)
            {
                logger.LogWarning("Image lookup requires manual artwork for {EntityId}: {MatchRationale}", entity.Id, ex.Message);
                return new(false, null, null, ex.Message, true, ImageLookupOutcome.AmbiguousMatch,
                    "Tmdb", strategy);
            }
            catch (ProviderLookupException ex)
            {
                Remember(new(false, null, null, ex.Message, !ImageRetryPolicy.IsRetryable(ex.Outcome),
                    ex.Outcome, ex.Provider, strategy, ex.RetryAt, confirmed));
            }
            catch (NotSupportedException ex)
            {
                Remember(new(false, null, null, ex.Message, true, ImageLookupOutcome.UnsupportedStrategy));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Image lookup attempt {Strategy} failed for {EntityId}", strategy, entity.Id);
                Remember(new(false, null, null, ex.Message, Outcome: ImageLookupOutcome.ProviderUnavailable,
                    AttemptedStrategy: strategy, ConfirmedMatch: confirmed));
            }
        }
        return (failure ?? new ImageLookupResult(false, null, null, "Entity lacks required lookup identifier", true,
            ImageLookupOutcome.MissingMetadata)) with { ConfirmedMatch = confirmed };
    }

    private async Task<IEnumerable<ImageCandidate>> LookupCandidatesAsync(MediaTypes mediaType, IdentifierType type,
        IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        return mediaType switch
        {
            MediaTypes.Books => (await strategyFactory.GetStrategy<BookResponse>(mediaType, type)
                .LookupAsync(type, parameters, cancellationToken)).SelectMany(b =>
                    (b.Artwork is { Count: > 0 } ? b.Artwork : [new BookArtwork(b.ImageUrl ?? "", "Provider artwork")])
                    .Select(artwork => new ImageCandidate(b.Title, artwork.Url,
                    b.PublishDate, b.Authors.Select(a => a.Name).ToList(), Provenance: b.Provenance,
                    Format: b.Format ?? "", Subtitle: b.Subtitle, Publishers: b.Publishers.Select(p => p.Name).ToList())
                    with { Provenance = b.Provenance == null ? null : b.Provenance with { ArtworkScope = artwork.Scope } })),
            MediaTypes.Movies => (await strategyFactory.GetStrategy<MovieResponse>(mediaType, type)
                .LookupAsync(type, parameters, cancellationToken)).Select(m => new ImageCandidate(m.Title, m.ImageUrl,
                    m.ReleaseDate, Provenance: m.Provenance)),
            MediaTypes.Games => (await strategyFactory.GetStrategy<GameResponse>(mediaType, type)
                .LookupAsync(type, parameters, cancellationToken)).Select(g => new ImageCandidate(g.Title, g.ImageUrl,
                    g.ReleaseDate, Platforms: g.Provenance?.Platforms ?? [g.Platform], Provenance: g.Provenance)),
            MediaTypes.Musics => (await strategyFactory.GetStrategy<MusicResponse>(mediaType, type)
                .LookupAsync(type, parameters, cancellationToken)).Select(m => new ImageCandidate(m.Title, m.ImageUrl,
                    m.ReleaseDate, [m.Artist], Provenance: m.Provenance, Format: m.Format)),
            _ => []
        };
    }

    private async Task<ImageLookupResult> TryDownloadImageAsync(string imageUrl, string entityType, string entityId,
        CancellationToken cancellationToken)
    {
        try
        {
            var image = await imageService.DownloadAndSaveImageAsync(imageUrl, entityType, entityId, cancellationToken);
            return new(true, imageUrl, image, null, Outcome: ImageLookupOutcome.Success);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            var cause = ex;
            while (cause.InnerException != null)
            {
                cause = cause.InnerException;
            }
            var missing = cause is HttpRequestException { StatusCode: System.Net.HttpStatusCode.NotFound };
            var invalid = ex is ArgumentException && cause is not HttpRequestException;
            return new(false, imageUrl, null, $"Download failed: {ex.Message}", missing || invalid,
                missing ? ImageLookupOutcome.MissingArtwork : invalid ? ImageLookupOutcome.InvalidArtwork
                    : ImageLookupOutcome.DownloadFailure);
        }
    }
}
