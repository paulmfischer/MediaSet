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

    /// <summary>Message of the exception thrown for FailUpsertFor.</summary>
    public string FailUpsertMessage { get; set; } = "upsert failed";

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
            throw new InvalidOperationException(FailUpsertMessage);
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

    public Task<IReadOnlyList<PlexItemData>> ListMoviesAsync(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyList<PlexItemData>>(_items.Values.Where(d => d.Scope == null).ToList());
        }
    }

    public Task<IReadOnlyList<PlexItemData>> ListSummariesAsync(IReadOnlyCollection<string> movieIds, CancellationToken cancellationToken)
    {
        SummaryReads++;
        if (SummariesFailure != null)
        {
            throw SummariesFailure;
        }
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyList<PlexItemData>>(_items.Values.Where(d => movieIds.Contains(d.Id)).ToList());
        }
    }

    public Task<IReadOnlyList<PlexItemData>> ListStatsAsync(CancellationToken cancellationToken)
    {
        if (StatsFailure != null)
        {
            throw StatsFailure;
        }
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyList<PlexItemData>>(_items.Values.ToList());
        }
    }

    public Task<IReadOnlyList<PlexItemData>> ListStatisticsAsync(CancellationToken cancellationToken)
    {
        if (StatsFailure != null)
        {
            throw StatsFailure;
        }
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyList<PlexItemData>>(_items.Values.ToList());
        }
    }

    /// <summary>When set, statistics reads throw it.</summary>
    public Exception? StatsFailure { get; set; }

    /// <summary>How many times the page summaries were read (the list join must be one query).</summary>
    public int SummaryReads { get; private set; }

    /// <summary>When set, summary reads throw it.</summary>
    public Exception? SummariesFailure { get; set; }

    public Task<IReadOnlyList<string>> ListIdsByFilterAsync(string filter, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var matching = PlexListFilters.Normalize(filter) switch
            {
                PlexListFilters.OnPlex or PlexListFilters.NotOnPlex => _items.Values.Where(d => d.OnPlex),
                PlexListFilters.Unwatched => _items.Values.Where(d => d.OnPlex && !d.Watched),
                PlexListFilters.Broken => _items.Values.Where(d => d.Broken),
                _ => throw new ArgumentException("bad filter")
            };
            return Task.FromResult<IReadOnlyList<string>>(matching.Select(d => d.Id).ToList());
        }
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
