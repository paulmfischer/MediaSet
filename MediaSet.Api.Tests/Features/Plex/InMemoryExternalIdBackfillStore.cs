using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Features.Plex.Services;

namespace MediaSet.Api.Tests.Features.Plex;

/// <summary>In-memory stand-in for the PlexBackfillAttempts collection, so tests need no MongoDB.</summary>
public sealed class InMemoryExternalIdBackfillStore : IExternalIdBackfillStore
{
    private readonly object _sync = new();
    private readonly Dictionary<string, DateTime> _attempts = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, DateTime> Attempts
    {
        get { lock (_sync) { return new Dictionary<string, DateTime>(_attempts); } }
    }

    public Task<IReadOnlyDictionary<string, DateTime>> GetAttemptsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Attempts);

    public Task RecordAttemptAsync(string movieId, DateTime attemptedAt, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            _attempts[movieId] = attemptedAt;
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(IReadOnlyCollection<string> movieIds, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            foreach (var id in movieIds)
            {
                _attempts.Remove(id);
            }
        }
        return Task.CompletedTask;
    }
}
