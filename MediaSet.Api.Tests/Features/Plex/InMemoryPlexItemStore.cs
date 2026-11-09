using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Features.Plex.Services;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace MediaSet.Api.Tests.Features.Plex;

/// <summary>In-memory stand-in for the PlexItems collection, so tests need no MongoDB.</summary>
public sealed class InMemoryPlexItemStore : IPlexItemStore
{
    private readonly object _sync = new();
    private readonly Dictionary<string, PlexItemData> _items = [];

    public bool EnsureCreatedCalled { get; private set; }

    /// <summary>When set, upserts of this movie id throw.</summary>
    public string? FailUpsertFor { get; set; }

    /// <summary>When set, deletes throw it.</summary>
    public Exception? DeleteFailure { get; set; }

    public IReadOnlyDictionary<string, PlexItemData> Items
    {
        get { lock (_sync) { return new Dictionary<string, PlexItemData>(_items); } }
    }

    public Task EnsureCreatedAsync(CancellationToken cancellationToken)
    {
        EnsureCreatedCalled = true;
        return Task.CompletedTask;
    }

    /// <summary>When set, runs after each read (with the movie id) and before its result is returned.</summary>
    public Func<string, Task>? AfterGet { get; set; }

    public async Task<PlexItemData?> GetAsync(string movieId, CancellationToken cancellationToken)
    {
        PlexItemData? found;
        lock (_sync)
        {
            found = _items.GetValueOrDefault(movieId);
        }
        if (AfterGet != null)
        {
            await AfterGet(movieId);
        }
        return found;
    }

    public Task UpsertAsync(PlexItemData data, CancellationToken cancellationToken)
    {
        if (data.Id == FailUpsertFor)
        {
            throw new InvalidOperationException("upsert failed");
        }
        lock (_sync)
        {
            _items[data.Id] = data;
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string movieId, CancellationToken cancellationToken)
    {
        if (DeleteFailure != null)
        {
            throw DeleteFailure;
        }
        lock (_sync)
        {
            _items.Remove(movieId);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListIdsAsync(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyList<string>>(_items.Keys.ToList());
        }
    }

    /// <summary>Swaps the registered store (and the cleanup that wraps it) for this instance.</summary>
    public static void Replace(IServiceCollection services, InMemoryPlexItemStore store)
    {
        foreach (var d in services.Where(d => d.ServiceType == typeof(IPlexItemStore)).ToList())
        {
            services.Remove(d);
        }
        services.AddSingleton<IPlexItemStore>(store);
    }

    /// <summary>A movie service that lists the given movies, for sync tests that need no MongoDB.</summary>
    public static Mock<IEntityService<Movie>> MovieService(params Movie[] movies)
    {
        var mock = new Mock<IEntityService<Movie>>();
        mock.Setup(s => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => movies.ToList());
        return mock;
    }
}
