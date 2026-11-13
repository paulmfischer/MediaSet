using Serilog.Core;
using Serilog.Events;

namespace MediaSet.Api.Shared.Extensions;

/// <summary>
/// Adds <c>Component</c> and <c>Provider</c> from the event's SourceContext (the logging class) when no scope set them,
/// so service and client logs are tagged without a scope at every call site. A scope always wins: a lookup client
/// called by the background image processor stays <c>Images</c>.
/// </summary>
public sealed class LogComponentEnricher : ILogEventEnricher
{
    private static readonly (string Prefix, string Component)[] _components =
    [
        ("MediaSet.Api.Features.Plex.", LogComponents.Plex),
        ("MediaSet.Api.Infrastructure.Plex.", LogComponents.Plex),
        ("MediaSet.Api.Features.Lookup.", LogComponents.Lookup),
        ("MediaSet.Api.Infrastructure.Lookup.", LogComponents.Lookup),
        ("MediaSet.Api.Features.Images.", LogComponents.Images),
        ("MediaSet.Api.Infrastructure.Storage.", LogComponents.Images),
        ("MediaSet.Api.Features.Statistics.", LogComponents.Stats),
    ];

    private static readonly (string Prefix, string Provider)[] _providers =
    [
        ("MediaSet.Api.Infrastructure.Lookup.Clients.Tmdb.", LogProviders.Tmdb),
        ("MediaSet.Api.Infrastructure.Lookup.Clients.Igdb.", LogProviders.Igdb),
        ("MediaSet.Api.Infrastructure.Lookup.Clients.OpenLibrary.", LogProviders.OpenLibrary),
        ("MediaSet.Api.Infrastructure.Lookup.Clients.MusicBrainz.", LogProviders.MusicBrainz),
        ("MediaSet.Api.Infrastructure.Lookup.Clients.UpcItemDb.", LogProviders.UpcItemDb),
        ("MediaSet.Api.Infrastructure.Lookup.Clients.GoogleBooks.", LogProviders.GoogleBooks),
    ];

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (!logEvent.Properties.TryGetValue("SourceContext", out var value)
            || value is not ScalarValue { Value: string source })
        {
            return;
        }

        if (Match(_components, source) is { } component)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(LogScopes.ComponentProperty, component));
        }
        if (Match(_providers, source) is { } provider)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(LogScopes.ProviderProperty, provider));
        }
    }

    private static string? Match((string Prefix, string Value)[] map, string source)
    {
        foreach (var (prefix, mapped) in map)
        {
            if (source.StartsWith(prefix, StringComparison.Ordinal))
            {
                return mapped;
            }
        }
        return null;
    }
}
