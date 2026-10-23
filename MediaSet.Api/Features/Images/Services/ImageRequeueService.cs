using MediaSet.Api.Features.Images.Models;
using MediaSet.Api.Infrastructure.Database;
using MediaSet.Api.Shared.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MediaSet.Api.Features.Images.Services;

public record ImageRequeueRequest(string OperationId, DateTime AttemptedBefore, int BatchSize = 100,
    string? AfterId = null, bool IncludeChangedTerminal = false, string[]? EntityIds = null);
public record ImageRequeueResult(int Scanned, int Queued, string? NextCursor, bool Complete);

public class ImageRequeueService(IDatabaseService database, IOptions<BackgroundImageLookupConfiguration> options)
{
    public Task<ImageRequeueResult> RequeueAsync(string entityType, ImageRequeueRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.OperationId) || request.OperationId.Length > 100
            || request.BatchSize is < 1 or > 500 || request.AttemptedBefore == default)
        {
            throw new ArgumentException("Provide an operation ID, attempted-before cutoff and batch size from 1 to 500");
        }
        if (request.AfterId != null && !ObjectId.TryParse(request.AfterId, out _)
            || request.EntityIds?.Any(id => !ObjectId.TryParse(id, out _)) == true)
        {
            throw new ArgumentException("Cursor and entity IDs must be valid MongoDB ObjectIds");
        }
        return entityType.ToLowerInvariant() switch
        {
            "books" => RequeueAsync<Book>(request, cancellationToken),
            "movies" => RequeueAsync<Movie>(request, cancellationToken),
            "games" => RequeueAsync<Game>(request, cancellationToken),
            "musics" => RequeueAsync<Music>(request, cancellationToken),
            _ => throw new ArgumentException("Unknown entity type")
        };
    }

    private async Task<ImageRequeueResult> RequeueAsync<T>(ImageRequeueRequest request,
        CancellationToken cancellationToken) where T : IEntity
    {
        var collection = database.GetCollection<T>();
        var f = Builders<T>.Filter;
        var filter = f.Eq(e => e.CoverImage, null) & f.Ne(e => e.ImageLookup, null)
            & f.Lte("ImageLookup.LookupAttemptedAt", request.AttemptedBefore.ToUniversalTime())
            & f.Ne("ImageLookup.LastOutcome", ImageLookupOutcome.Success)
            & f.Ne("ImageLookup.LastOutcome", ImageLookupOutcome.Queued)
            & f.Ne("ImageLookup.RequeueOperationId", request.OperationId);
        if (request.AfterId != null)
        {
            filter &= f.Gt(e => e.Id, request.AfterId);
        }
        if (request.EntityIds != null)
        {
            filter &= f.In(e => e.Id, request.EntityIds);
        }
        var entities = await collection.Find(filter).SortBy(e => e.Id).Limit(request.BatchSize)
            .ToListAsync(cancellationToken);
        var queued = 0;
        foreach (var entity in entities)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ImageRetryPolicy.CanRequeue(entity, options.Value, request.IncludeChangedTerminal))
            {
                continue;
            }
            var state = entity.ImageLookup!;
            var update = Builders<T>.Update
                .Set("ImageLookup.LastOutcome", ImageLookupOutcome.Queued)
                .Set("ImageLookup.NextEligibleAt", DateTime.UtcNow)
                .Set("ImageLookup.PermanentFailure", false)
                .Set("ImageLookup.IsBacklogRecovery", true)
                .Set("ImageLookup.RequeueOperationId", request.OperationId);
            var unchanged = filter & f.Eq(e => e.Id, entity.Id)
                & f.Eq("ImageLookup.LookupAttemptedAt", state.LookupAttemptedAt)
                & f.Eq("ImageLookup.LastOutcome", state.LastOutcome);
            var result = await collection.UpdateOneAsync(unchanged, update, cancellationToken: cancellationToken);
            queued += (int)result.ModifiedCount;
        }
        return new(entities.Count, queued, entities.LastOrDefault()?.Id, entities.Count < request.BatchSize);
    }
}
