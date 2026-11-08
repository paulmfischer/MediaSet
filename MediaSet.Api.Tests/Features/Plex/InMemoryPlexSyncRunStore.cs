using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Features.Plex.Services;
using MediaSet.Api.Features.Plex.Models;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;

namespace MediaSet.Api.Tests.Features.Plex;

/// <summary>In-memory stand-in for the PlexSyncRuns collection, so HTTP-level tests need no MongoDB.</summary>
public sealed class InMemoryPlexSyncRunStore : IPlexSyncRunStore
{
    private readonly object _sync = new();
    private readonly List<PlexSyncRun> _runs = [];

    public bool EnsureCreatedCalled { get; private set; }

    /// <summary>Awaited before an insert stores the run and assigns its id, to simulate a slow database.</summary>
    public Func<Task>? BeforeInsert { get; set; }

    /// <summary>When set, inserts throw it.</summary>
    public Exception? InsertFailure { get; set; }

    public IReadOnlyList<PlexSyncRun> Runs
    {
        get { lock (_sync) { return _runs.Select(r => r.Clone()).ToList(); } }
    }

    /// <summary>Swaps the registered store for an in-memory one (or the given instance).</summary>
    public static InMemoryPlexSyncRunStore Replace(IServiceCollection services, InMemoryPlexSyncRunStore? store = null)
    {
        store ??= new InMemoryPlexSyncRunStore();
        foreach (var d in services.Where(d => d.ServiceType == typeof(IPlexSyncRunStore)).ToList())
        {
            services.Remove(d);
        }
        services.AddSingleton<IPlexSyncRunStore>(store);
        return store;
    }

    public Task EnsureCreatedAsync(CancellationToken cancellationToken)
    {
        EnsureCreatedCalled = true;
        return Task.CompletedTask;
    }

    public Task<long> InterruptInProgressAsync(string message, DateTime endedAt, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var open = _runs.Where(r => PlexSyncRunStates.IsInProgress(r.State)).ToList();
            foreach (var run in open)
            {
                run.State = PlexSyncRunStates.Interrupted;
                run.EndedAt = endedAt;
                run.Message = message;
            }
            return Task.FromResult((long)open.Count);
        }
    }

    public async Task InsertAsync(PlexSyncRun run, CancellationToken cancellationToken)
    {
        if (BeforeInsert != null)
        {
            await BeforeInsert();
        }
        if (InsertFailure != null)
        {
            throw InsertFailure;
        }
        lock (_sync)
        {
            run.Id ??= ObjectId.GenerateNewId().ToString();
            _runs.Add(run.Clone());
        }
    }

    public Task UpdateAsync(PlexSyncRun run, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var index = _runs.FindIndex(r => r.Id == run.Id);
            _runs[index] = run.Clone();
        }
        return Task.CompletedTask;
    }

    public Task<PlexSyncRun?> GetLastAsync(string entityType, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult(_runs
                .Where(r => r.EntityType == entityType)
                .OrderByDescending(r => r.StartedAt)
                .Select(r => r.Clone())
                .FirstOrDefault());
        }
    }
}
