using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Infrastructure.Database;
using MediaSet.Api.Infrastructure.Plex;
using MongoDB.Driver;

namespace MediaSet.Api.Features.Plex.Services;

public interface IPlexItemStore
{
    /// <summary>Creates the PlexItems collection and its indexes when missing.</summary>
    Task EnsureCreatedAsync(CancellationToken cancellationToken);

    Task<PlexItemData?> GetAsync(string entryId, CancellationToken cancellationToken);

    /// <summary>The document of the entry, only when it is of the given entity type (movies include TV entries); null for a document of the other type, since movie and music ids share one id space.</summary>
    Task<PlexItemData?> GetAsync(string entryId, string entityType, CancellationToken cancellationToken);
    Task UpsertAsync(PlexItemData data, CancellationToken cancellationToken);
    Task DeleteAsync(string entryId, CancellationToken cancellationToken);

    /// <summary>
    /// Every stored movie document (TV entries are excluded), with only the fields needed to decide whether a Plex item
    /// changed: id, Plex item id, file size and PlexUpdatedAt.
    /// </summary>
    Task<IReadOnlyList<PlexItemData>> ListMoviesAsync(CancellationToken cancellationToken);

    /// <summary>The documents of the given entry ids in one query, with only the fields the list summary needs.</summary>
    Task<IReadOnlyList<PlexItemData>> ListSummariesAsync(IReadOnlyCollection<string> entryIds, CancellationToken cancellationToken);

    /// <summary>Every stored document with only the fields the statistics need (OnPlex, Broken, Watched, Resolution).</summary>
    Task<IReadOnlyList<PlexItemData>> ListStatsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Every stored document projected to the fields the statistics page needs: no file path, title, season list or
    /// episode id, so a TV entry's episodes carry only their stream, size, duration and watched fields.
    /// </summary>
    Task<IReadOnlyList<PlexItemData>> ListStatisticsProjectionAsync(CancellationToken cancellationToken);

    /// <summary>The music documents with only the fields the music statistics read (no file path or deep link).</summary>
    Task<IReadOnlyList<PlexItemData>> ListMusicStatisticsProjectionAsync(CancellationToken cancellationToken);

    /// <summary>Ids of the documents of an entity type in a list filter state (see <see cref="PlexListFilters"/>); movies include TV entries.</summary>
    Task<IReadOnlyList<string>> ListIdsByFilterAsync(string filter, string entityType, CancellationToken cancellationToken);

    /// <summary>Ids of every stored document of an entity type (movies include TV entries), used to purge documents of entries that no longer exist.</summary>
    Task<IReadOnlyList<string>> ListIdsAsync(string entityType, CancellationToken cancellationToken);
}

public class PlexItemStore(IDatabaseService database) : IPlexItemStore
{
    public const string CollectionName = "PlexItems";
    public const string PlexItemIdIndexName = "PlexItemId";

    /// <summary>Movie and TV documents carry no EntityType; music documents are told apart by it (Mongo matches a missing field as null).</summary>
    private static FilterDefinition<PlexItemData> OfEntityType(string entityType) =>
        PlexEntityTypes.Normalize(entityType) == PlexEntityTypes.Music
            ? Builders<PlexItemData>.Filter.Eq(d => d.EntityType, PlexEntityTypes.Music)
            : Builders<PlexItemData>.Filter.Eq(d => d.EntityType, null);

    private IMongoCollection<PlexItemData> Collection => database.GetCollection<PlexItemData>(CollectionName);

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken)
    {
        // The document id is the entry id (a movie or music id), so Mongo's built-in unique _id index covers the entity id.
        // Creating an index also creates the collection when it does not exist.
        await Collection.Indexes.CreateOneAsync(
            new CreateIndexModel<PlexItemData>(
                Builders<PlexItemData>.IndexKeys.Ascending(d => d.PlexItemId),
                new CreateIndexOptions { Name = PlexItemIdIndexName }),
            cancellationToken: cancellationToken);
    }

    public async Task<PlexItemData?> GetAsync(string entryId, CancellationToken cancellationToken) =>
        await Collection.Find(d => d.Id == entryId).FirstOrDefaultAsync(cancellationToken);

    public async Task<PlexItemData?> GetAsync(string entryId, string entityType, CancellationToken cancellationToken) =>
        await Collection.Find(Builders<PlexItemData>.Filter.And(Builders<PlexItemData>.Filter.Eq(d => d.Id, entryId), OfEntityType(entityType)))
            .FirstOrDefaultAsync(cancellationToken);

    public Task UpsertAsync(PlexItemData data, CancellationToken cancellationToken) =>
        Collection.ReplaceOneAsync(d => d.Id == data.Id, data, new ReplaceOptions { IsUpsert = true }, cancellationToken);

    public Task DeleteAsync(string entryId, CancellationToken cancellationToken) =>
        Collection.DeleteOneAsync(d => d.Id == entryId, cancellationToken);

    public async Task<IReadOnlyList<PlexItemData>> ListMoviesAsync(CancellationToken cancellationToken) =>
        await Collection.Find(Builders<PlexItemData>.Filter.And(OfEntityType(PlexEntityTypes.Movies), Builders<PlexItemData>.Filter.Eq(d => d.Scope, null)))
            .Project<PlexItemData>(Builders<PlexItemData>.Projection
                .Include(d => d.PlexItemId)
                .Include(d => d.SizeBytes)
                .Include(d => d.PlexUpdatedAt))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// The fields the list summary needs. EntityType must stay in it: an inclusion projection drops every other field, and
    /// the music lookup tells music documents from movie ones by it.
    /// </summary>
    public static ProjectionDefinition<PlexItemData> SummaryProjection { get; } = Builders<PlexItemData>.Projection
        .Include(d => d.EntityType)
        .Include(d => d.OnPlex)
        .Include(d => d.Broken)
        .Include(d => d.Watched)
        .Include(d => d.Resolution)
        .Include(d => d.PlayCount)
        .Include(d => d.LastPlayedAt)
        .Include(d => d.TracksPresent)
        .Include(d => d.TracksExpected)
        .Include(d => d.AudioCodec)
        .Include(d => d.Bitrate);

    public async Task<IReadOnlyList<PlexItemData>> ListSummariesAsync(IReadOnlyCollection<string> entryIds, CancellationToken cancellationToken) =>
        await Collection.Find(Builders<PlexItemData>.Filter.In(d => d.Id, entryIds))
            .Project<PlexItemData>(SummaryProjection)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PlexItemData>> ListStatsAsync(CancellationToken cancellationToken) =>
        await Collection.Find(OfEntityType(PlexEntityTypes.Movies))
            .Project<PlexItemData>(Builders<PlexItemData>.Projection
                .Include(d => d.OnPlex)
                .Include(d => d.Broken)
                .Include(d => d.Watched)
                .Include(d => d.Resolution))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PlexItemData>> ListStatisticsProjectionAsync(CancellationToken cancellationToken)
    {
        var p = Builders<PlexItemData>.Projection;
        var episodeFields = new[] { "OnPlex", "Watched", "Resolution", "Hdr", "VideoCodec", "BestAudioTrack", "SubtitleLanguages", "SizeBytes", "DurationSeconds" };
        var projection = p.Combine(
            p.Include(d => d.OnPlex)
                .Include(d => d.Broken)
                .Include(d => d.Watched)
                .Include(d => d.Resolution)
                .Include(d => d.Hdr)
                .Include(d => d.VideoCodec)
                .Include(d => d.BestAudioTrack)
                .Include(d => d.SubtitleLanguages)
                .Include(d => d.ViewCount)
                .Include(d => d.SizeBytes)
                .Include(d => d.DurationSeconds)
                .Include(d => d.Scope)
                .Include("Rollup.EpisodesPresent")
                .Include("Rollup.EpisodesExpected")
                .Include("Rollup.WatchedCount"),
            p.Combine(episodeFields.Select(f => p.Include($"Episodes.{f}"))));
        return await Collection.Find(OfEntityType(PlexEntityTypes.Movies))
            .Project<PlexItemData>(projection)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PlexItemData>> ListMusicStatisticsProjectionAsync(CancellationToken cancellationToken) =>
        await Collection.Find(OfEntityType(PlexEntityTypes.Music))
            .Project<PlexItemData>(Builders<PlexItemData>.Projection
                .Include(d => d.OnPlex)
                .Include(d => d.Broken)
                .Include(d => d.PlayCount)
                .Include(d => d.LastPlayedAt)
                .Include(d => d.DateAdded)
                .Include(d => d.TracksPresent)
                .Include(d => d.TracksExpected)
                .Include(d => d.AudioCodec)
                .Include(d => d.Bitrate)
                .Include(d => d.SizeBytes))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> ListIdsByFilterAsync(string filter, string entityType, CancellationToken cancellationToken)
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
        return await Collection.Find(f.And(OfEntityType(entityType), query)).Project(d => d.Id).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListIdsAsync(string entityType, CancellationToken cancellationToken) =>
        await Collection.Find(OfEntityType(entityType))
            .Project(d => d.Id)
            .ToListAsync(cancellationToken);
}
