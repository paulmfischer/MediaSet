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

    /// <summary>The documents of the given movie ids in one query, with only the fields the list summary needs.</summary>
    Task<IReadOnlyList<PlexItemData>> ListSummariesAsync(IReadOnlyCollection<string> movieIds, CancellationToken cancellationToken);

    /// <summary>Every stored document with only the fields the statistics need (OnPlex, Broken, Watched, Resolution).</summary>
    Task<IReadOnlyList<PlexItemData>> ListStatsAsync(CancellationToken cancellationToken);

    /// <summary>Ids of the documents in a list filter state (see <see cref="PlexListFilters"/>), for movies and TV entries.</summary>
    Task<IReadOnlyList<string>> ListIdsByFilterAsync(string filter, CancellationToken cancellationToken);

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

    public async Task<IReadOnlyList<PlexItemData>> ListSummariesAsync(IReadOnlyCollection<string> movieIds, CancellationToken cancellationToken) =>
        await Collection.Find(Builders<PlexItemData>.Filter.In(d => d.Id, movieIds))
            .Project<PlexItemData>(Builders<PlexItemData>.Projection
                .Include(d => d.OnPlex)
                .Include(d => d.Broken)
                .Include(d => d.Watched)
                .Include(d => d.Resolution))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PlexItemData>> ListStatsAsync(CancellationToken cancellationToken) =>
        await Collection.Find(FilterDefinition<PlexItemData>.Empty)
            .Project<PlexItemData>(Builders<PlexItemData>.Projection
                .Include(d => d.OnPlex)
                .Include(d => d.Broken)
                .Include(d => d.Watched)
                .Include(d => d.Resolution))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> ListIdsByFilterAsync(string filter, CancellationToken cancellationToken)
    {
        var f = Builders<PlexItemData>.Filter;
        // notOnPlex lists the ids that ARE on Plex; the caller excludes them
        var query = PlexListFilters.Normalize(filter) switch
        {
            PlexListFilters.OnPlex or PlexListFilters.NotOnPlex => f.Eq(d => d.OnPlex, true),
            PlexListFilters.Unwatched => f.And(f.Eq(d => d.OnPlex, true), f.Eq(d => d.Watched, false)),
            PlexListFilters.Broken => f.Eq(d => d.Broken, true),
            _ => throw new ArgumentException($"Invalid plex filter '{filter}'.")
        };
        return await Collection.Find(query).Project(d => d.Id).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListIdsAsync(CancellationToken cancellationToken) =>
        await Collection.Find(FilterDefinition<PlexItemData>.Empty)
            .Project(d => d.Id)
            .ToListAsync(cancellationToken);
}
