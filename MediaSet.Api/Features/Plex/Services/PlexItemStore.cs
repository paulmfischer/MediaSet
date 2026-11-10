using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Infrastructure.Database;
using MongoDB.Driver;

namespace MediaSet.Api.Features.Plex.Services;

public interface IPlexItemStore
{
    /// <summary>Creates the PlexItems collection and its indexes when missing.</summary>
    Task EnsureCreatedAsync(CancellationToken cancellationToken);

    Task<PlexItemData?> GetAsync(string movieId, CancellationToken cancellationToken);
    Task UpsertAsync(PlexItemData data, CancellationToken cancellationToken);
    Task DeleteAsync(string movieId, CancellationToken cancellationToken);

    /// <summary>
    /// Every stored movie document (TV entries are excluded), with only the fields needed to decide whether a Plex item
    /// changed: id, Plex item id, file size and PlexUpdatedAt.
    /// </summary>
    Task<IReadOnlyList<PlexItemData>> ListMoviesAsync(CancellationToken cancellationToken);

    /// <summary>Ids of every stored document, used to purge documents of movies that no longer exist.</summary>
    Task<IReadOnlyList<string>> ListIdsAsync(CancellationToken cancellationToken);
}

public class PlexItemStore(IDatabaseService database) : IPlexItemStore
{
    public const string CollectionName = "PlexItems";
    public const string PlexItemIdIndexName = "PlexItemId";

    private IMongoCollection<PlexItemData> Collection => database.GetCollection<PlexItemData>(CollectionName);

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken)
    {
        // The document id is the movie id, so Mongo's built-in unique _id index covers the entity id.
        // Creating an index also creates the collection when it does not exist.
        await Collection.Indexes.CreateOneAsync(
            new CreateIndexModel<PlexItemData>(
                Builders<PlexItemData>.IndexKeys.Ascending(d => d.PlexItemId),
                new CreateIndexOptions { Name = PlexItemIdIndexName }),
            cancellationToken: cancellationToken);
    }

    public async Task<PlexItemData?> GetAsync(string movieId, CancellationToken cancellationToken) =>
        await Collection.Find(d => d.Id == movieId).FirstOrDefaultAsync(cancellationToken);

    public Task UpsertAsync(PlexItemData data, CancellationToken cancellationToken) =>
        Collection.ReplaceOneAsync(d => d.Id == data.Id, data, new ReplaceOptions { IsUpsert = true }, cancellationToken);

    public Task DeleteAsync(string movieId, CancellationToken cancellationToken) =>
        Collection.DeleteOneAsync(d => d.Id == movieId, cancellationToken);

    public async Task<IReadOnlyList<PlexItemData>> ListMoviesAsync(CancellationToken cancellationToken) =>
        await Collection.Find(d => d.Scope == null)
            .Project<PlexItemData>(Builders<PlexItemData>.Projection
                .Include(d => d.PlexItemId)
                .Include(d => d.SizeBytes)
                .Include(d => d.PlexUpdatedAt))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> ListIdsAsync(CancellationToken cancellationToken) =>
        await Collection.Find(FilterDefinition<PlexItemData>.Empty)
            .Project(d => d.Id)
            .ToListAsync(cancellationToken);
}
