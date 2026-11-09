using MediaSet.Api.Infrastructure.Database;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace MediaSet.Api.Features.Plex.Services;

/// <summary>When the backfill last searched TMDB for a movie. The document id is the movie id. Kept apart from the movie.</summary>
public class ExternalIdBackfillAttempt
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime LastAttemptAt { get; set; }
}

public interface IExternalIdBackfillStore
{
    /// <summary>Last attempt time per movie id.</summary>
    Task<IReadOnlyDictionary<string, DateTime>> GetAttemptsAsync(CancellationToken cancellationToken);

    Task RecordAttemptAsync(string movieId, DateTime attemptedAt, CancellationToken cancellationToken);

    Task DeleteAsync(IReadOnlyCollection<string> movieIds, CancellationToken cancellationToken);
}

public class ExternalIdBackfillStore(IDatabaseService database) : IExternalIdBackfillStore
{
    public const string CollectionName = "PlexBackfillAttempts";

    // The built-in unique _id index covers the movie id; the collection is created on the first write
    private IMongoCollection<ExternalIdBackfillAttempt> Collection => database.GetCollection<ExternalIdBackfillAttempt>(CollectionName);

    public async Task<IReadOnlyDictionary<string, DateTime>> GetAttemptsAsync(CancellationToken cancellationToken) =>
        (await Collection.Find(FilterDefinition<ExternalIdBackfillAttempt>.Empty).ToListAsync(cancellationToken))
            .ToDictionary(a => a.Id, a => a.LastAttemptAt, StringComparer.Ordinal);

    public Task RecordAttemptAsync(string movieId, DateTime attemptedAt, CancellationToken cancellationToken) =>
        Collection.ReplaceOneAsync(
            a => a.Id == movieId,
            new ExternalIdBackfillAttempt { Id = movieId, LastAttemptAt = attemptedAt },
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);

    public Task DeleteAsync(IReadOnlyCollection<string> movieIds, CancellationToken cancellationToken) =>
        movieIds.Count == 0
            ? Task.CompletedTask
            : Collection.DeleteManyAsync(a => movieIds.Contains(a.Id), cancellationToken);
}
