using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Infrastructure.Database;
using MongoDB.Driver;

namespace MediaSet.Api.Features.Plex.Services;

public interface IPlexSyncRunStore
{
    /// <summary>Creates the PlexSyncRuns collection and its index when missing.</summary>
    Task EnsureCreatedAsync(CancellationToken cancellationToken);

    /// <summary>Closes every queued or running record as interrupted. Returns how many were closed.</summary>
    Task<long> InterruptInProgressAsync(string message, DateTime endedAt, CancellationToken cancellationToken);

    Task InsertAsync(PlexSyncRun run, CancellationToken cancellationToken);
    Task UpdateAsync(PlexSyncRun run, CancellationToken cancellationToken);

    /// <summary>The most recently started run for an entity type, or null.</summary>
    Task<PlexSyncRun?> GetLastAsync(string entityType, CancellationToken cancellationToken);
}

public class PlexSyncRunStore(IDatabaseService database) : IPlexSyncRunStore
{
    public const string IndexName = "EntityType_StartedAt";

    private IMongoCollection<PlexSyncRun> Collection => database.GetCollection<PlexSyncRun>();

    private volatile bool _ensured;

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken)
    {
        // Creating an index also creates the collection when it does not exist
        var keys = Builders<PlexSyncRun>.IndexKeys
            .Ascending(r => r.EntityType)
            .Descending(r => r.StartedAt);
        await Collection.Indexes.CreateOneAsync(
            new CreateIndexModel<PlexSyncRun>(keys, new CreateIndexOptions { Name = IndexName }),
            cancellationToken: cancellationToken);
        _ensured = true;
    }

    public async Task<long> InterruptInProgressAsync(string message, DateTime endedAt, CancellationToken cancellationToken)
    {
        var filter = Builders<PlexSyncRun>.Filter.In(r => r.State, new[] { PlexSyncRunStates.Queued, PlexSyncRunStates.Running });
        var update = Builders<PlexSyncRun>.Update
            .Set(r => r.State, PlexSyncRunStates.Interrupted)
            .Set(r => r.EndedAt, endedAt)
            .Set(r => r.Message, message);
        var result = await Collection.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount;
    }

    public async Task InsertAsync(PlexSyncRun run, CancellationToken cancellationToken)
    {
        // The startup ensure may have failed (MongoDB briefly down): retry it before the first write
        if (!_ensured)
        {
            await EnsureCreatedAsync(cancellationToken);
        }

        await Collection.InsertOneAsync(run, cancellationToken: cancellationToken);
    }

    public Task UpdateAsync(PlexSyncRun run, CancellationToken cancellationToken) =>
        Collection.ReplaceOneAsync(r => r.Id == run.Id, run, cancellationToken: cancellationToken);

    public async Task<PlexSyncRun?> GetLastAsync(string entityType, CancellationToken cancellationToken) =>
        await Collection.Find(r => r.EntityType == entityType)
            .SortByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
}
