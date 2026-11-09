using System.Collections.Concurrent;

namespace MediaSet.Api.Features.Plex.Services;

/// <summary>Next scheduled run per entity type, written by the scheduler and read by the status endpoints.</summary>
public interface IPlexScheduleInfo
{
    DateTimeOffset? GetNextRun(string entityType);

    void SetNextRun(string entityType, DateTimeOffset? nextRun);
}

public sealed class PlexScheduleInfo : IPlexScheduleInfo
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _next = new(StringComparer.OrdinalIgnoreCase);

    public DateTimeOffset? GetNextRun(string entityType) =>
        _next.TryGetValue(entityType, out var next) ? next : null;

    public void SetNextRun(string entityType, DateTimeOffset? nextRun)
    {
        if (nextRun is { } value)
        {
            _next[entityType] = value;
        }
        else
        {
            _next.TryRemove(entityType, out _);
        }
    }
}
