using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace MediaSet.Api.Infrastructure.Plex;

public class PlexClient : IPlexClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    /// <summary>Safety cap on pages per library, so a misbehaving server cannot loop the client forever.</summary>
    public const int MaxPages = 10_000;

    private readonly HttpClient _httpClient;
    private readonly PlexConfiguration _configuration;
    private readonly ILogger<PlexClient> _logger;
    private string? _machineIdentifier;

    public PlexClient(HttpClient httpClient, IOptions<PlexConfiguration> configuration, ILogger<PlexClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PlexLibrary>> GetLibrariesAsync(CancellationToken cancellationToken)
    {
        var root = await GetAsync<SectionsRoot>("library/sections", null, cancellationToken);
        return (root.MediaContainer?.Directory ?? [])
            .Where(d => !string.IsNullOrEmpty(d.Key))
            .Select(d => new PlexLibrary(d.Key!, d.Title ?? string.Empty, d.Type ?? string.Empty))
            .ToList();
    }

    public async Task<IReadOnlyList<PlexLibrary>> GetLibrariesForEntityTypeAsync(string entityType, CancellationToken cancellationToken)
    {
        var plexType = PlexEntityTypes.LibraryType(entityType)
            ?? throw new ArgumentException("Unsupported Plex entity type.", nameof(entityType));

        var libraries = (await GetLibrariesAsync(cancellationToken))
            .Where(l => string.Equals(l.Type, plexType, StringComparison.OrdinalIgnoreCase));

        if (_configuration.Entities.TryGetValue(entityType, out var entity) && entity.LibrarySectionIds.Count > 0)
        {
            var wanted = new HashSet<string>(entity.LibrarySectionIds.Select(i => i.Trim()), StringComparer.Ordinal);
            libraries = libraries.Where(l => wanted.Contains(l.Id));
        }

        return libraries.ToList();
    }

    public async Task<IReadOnlyList<PlexItem>> GetItemsAsync(PlexLibrary library, string? titleContains, bool includeDeepLinks, CancellationToken cancellationToken)
    {
        var pageSize = _configuration.PageSize > 0 ? _configuration.PageSize : 100;
        // The server identity is only needed to build deep links
        var serverId = includeDeepLinks ? await GetMachineIdentifierAsync(cancellationToken) : null;
        var path = $"library/sections/{Uri.EscapeDataString(library.Id)}/all";
        if (!string.IsNullOrWhiteSpace(titleContains))
        {
            path += $"?title={Uri.EscapeDataString(titleContains)}";
        }

        var items = new List<PlexItem>();
        var start = 0;
        for (var page = 0; page < MaxPages; page++)
        {
            var root = await GetAsync<ItemsRoot>(path, (start, pageSize), cancellationToken);
            var container = root.MediaContainer;
            var metadata = container?.Metadata ?? [];
            items.AddRange(metadata.Select(m => MapItem(m, library, DeepLinkBase(serverId))));

            start += metadata.Count;
            if (metadata.Count < pageSize || (container?.TotalSize.HasValue == true && start >= container.TotalSize))
            {
                return items;
            }
        }

        _logger.LogWarning("Plex library paging exceeded {MaxPages} pages", MaxPages);
        throw new PlexClientException(null, "Plex returned too many pages");
    }

    private async Task<string?> GetMachineIdentifierAsync(CancellationToken cancellationToken)
    {
        if (_machineIdentifier != null)
        {
            return _machineIdentifier;
        }

        var root = await GetAsync<IdentityRoot>("identity", null, cancellationToken);
        _machineIdentifier = root.MediaContainer?.MachineIdentifier;
        return _machineIdentifier;
    }

    private async Task<T> GetAsync<T>(string path, (int Start, int Size)? page, CancellationToken cancellationToken) where T : class, new()
    {
        // GET only; the token travels in the X-Plex-Token header of the HttpClient, never in the URL
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        if (page is { } p)
        {
            request.Headers.TryAddWithoutValidation("X-Plex-Container-Start", p.Start.ToString());
            request.Headers.TryAddWithoutValidation("X-Plex-Container-Size", p.Size.ToString());
        }

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                LogFailure(null, (int)response.StatusCode);
                throw new PlexClientException((int)response.StatusCode, "Plex request failed");
            }

            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
                ?? throw new PlexClientException(null, "Plex returned an empty response");
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogFailure(ex, null);
            throw new PlexClientException(null, "Plex request timed out");
        }
        catch (HttpRequestException ex)
        {
            LogFailure(ex, null);
            throw new PlexClientException(null, "Plex server could not be reached");
        }
        catch (JsonException ex)
        {
            LogFailure(ex, null);
            throw new PlexClientException(null, "Plex returned an unreadable response");
        }
    }

    // Only the status code and exception type are logged: never the URL, headers, token or inner message
    private void LogFailure(Exception? exception, int? statusCode) =>
        _logger.LogWarning(
            "Plex request failed (status code {StatusCode}, exception {ExceptionType})",
            statusCode,
            exception?.GetType().Name);

    private static PlexItem MapItem(RawMetadata m, PlexLibrary library, string? linkBase) => new(
        m.RatingKey ?? string.Empty,
        m.Type ?? string.Empty,
        m.Title ?? string.Empty,
        m.Year,
        library.Id,
        library.Title,
        m.AddedAt is > 0 ? DateTimeOffset.FromUnixTimeSeconds(m.AddedAt.Value) : null,
        DeepLink(linkBase, m.RatingKey),
        ParseGuids(m.Guid),
        (m.Media ?? []).Select(MapVersion).ToList());

    private string? DeepLinkBase(string? serverId) =>
        string.IsNullOrEmpty(serverId) || _httpClient.BaseAddress == null
            ? null
            : $"{_httpClient.BaseAddress.ToString().TrimEnd('/')}/web/index.html#!/server/{serverId}/details?key=";

    private static string? DeepLink(string? linkBase, string? ratingKey) =>
        linkBase == null || string.IsNullOrEmpty(ratingKey)
            ? null
            : linkBase + Uri.EscapeDataString($"/library/metadata/{ratingKey}");

    private static PlexGuids ParseGuids(List<RawGuid>? guids)
    {
        string? Find(string scheme) => guids?
            .Select(g => g.Id)
            .Where(id => id != null && id.StartsWith(scheme + "://", StringComparison.OrdinalIgnoreCase))
            .Select(id => id![(scheme.Length + 3)..])
            .FirstOrDefault(v => v.Length > 0);

        return new PlexGuids(Find("tmdb"), Find("imdb"), Find("tvdb"));
    }

    private static PlexVersion MapVersion(RawMedia media)
    {
        var part = media.Part?.FirstOrDefault();
        var streams = part?.Stream ?? [];
        var video = streams.FirstOrDefault(s => s.StreamType == 1);

        return new PlexVersion(
            media.VideoResolution,
            video != null && IsHdr(video),
            media.VideoCodec ?? video?.Codec,
            streams.Where(s => s.StreamType == 2)
                .Select(s => new PlexAudioTrack(s.Codec, s.Channels, s.LanguageCode ?? s.Language))
                .ToList(),
            streams.Where(s => s.StreamType == 3)
                .Select(s => new PlexSubtitle(s.LanguageCode ?? s.Language, !string.IsNullOrEmpty(s.Key)))
                .ToList(),
            media.Duration is > 0 ? TimeSpan.FromMilliseconds(media.Duration.Value) : null,
            media.Container ?? part?.Container,
            part?.File,
            part?.Size);
    }

    private static bool IsHdr(RawStream video) =>
        video.DoviPresent == true
        || string.Equals(video.ColorTrc, "smpte2084", StringComparison.OrdinalIgnoreCase)
        || string.Equals(video.ColorTrc, "arib-std-b67", StringComparison.OrdinalIgnoreCase)
        || (video.DisplayTitle?.Contains("HDR", StringComparison.OrdinalIgnoreCase) ?? false);

    // Raw Plex JSON shapes
    private sealed class SectionsRoot { [JsonPropertyName("MediaContainer")] public SectionsContainer? MediaContainer { get; set; } }
    private sealed class SectionsContainer { [JsonPropertyName("Directory")] public List<RawDirectory>? Directory { get; set; } }
    private sealed class RawDirectory { public string? Key { get; set; } public string? Title { get; set; } public string? Type { get; set; } }
    private sealed class IdentityRoot { [JsonPropertyName("MediaContainer")] public IdentityContainer? MediaContainer { get; set; } }
    private sealed class IdentityContainer { public string? MachineIdentifier { get; set; } }
    private sealed class ItemsRoot { [JsonPropertyName("MediaContainer")] public ItemsContainer? MediaContainer { get; set; } }
    private sealed class ItemsContainer
    {
        public int? Size { get; set; }
        public int? TotalSize { get; set; }
        [JsonPropertyName("Metadata")] public List<RawMetadata>? Metadata { get; set; }
    }
    private sealed class RawMetadata
    {
        public string? RatingKey { get; set; }
        public string? Type { get; set; }
        public string? Title { get; set; }
        public int? Year { get; set; }
        public long? AddedAt { get; set; }
        [JsonPropertyName("Guid")] public List<RawGuid>? Guid { get; set; }
        [JsonPropertyName("Media")] public List<RawMedia>? Media { get; set; }
    }
    private sealed class RawGuid { public string? Id { get; set; } }
    private sealed class RawMedia
    {
        public long? Duration { get; set; }
        public string? VideoResolution { get; set; }
        public string? VideoCodec { get; set; }
        public string? Container { get; set; }
        [JsonPropertyName("Part")] public List<RawPart>? Part { get; set; }
    }
    private sealed class RawPart
    {
        public string? File { get; set; }
        public long? Size { get; set; }
        public string? Container { get; set; }
        [JsonPropertyName("Stream")] public List<RawStream>? Stream { get; set; }
    }
    private sealed class RawStream
    {
        public int? StreamType { get; set; }
        public string? Codec { get; set; }
        public int? Channels { get; set; }
        public string? LanguageCode { get; set; }
        public string? Language { get; set; }
        public string? Key { get; set; }
        public string? ColorTrc { get; set; }
        [JsonPropertyName("DOVIPresent")]
        public bool? DoviPresent { get; set; }
        public string? DisplayTitle { get; set; }
    }
}
