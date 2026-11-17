using MediaSet.Api.Shared.Models;
using MongoDB.Driver;

namespace MediaSet.Api.Features.Entities.Services;

public enum EntityWriteStatus
{
    /// <summary>The entity was saved (its cover image may still have failed; see <c>ImageError</c>).</summary>
    Success,
    /// <summary>A create carried no data.</summary>
    EmptyEntity,
    /// <summary>A Movie or Music entry has a malformed ExternalId.</summary>
    InvalidExternalId,
    /// <summary>An update's path id differs from the entity's id.</summary>
    IdMismatch,
    /// <summary>An update targets an entity that does not exist.</summary>
    NotFound,
}

/// <summary>Outcome of a create or update.</summary>
/// <param name="Status">Whether the entity was saved, or why not.</param>
/// <param name="Entity">The saved entity (Success only).</param>
/// <param name="ImageError">An <c>ImageErrorCodes</c> value when the entity was saved but its cover image was not.</param>
public sealed record EntityWriteResult<TEntity>(EntityWriteStatus Status, TEntity? Entity = default, string? ImageError = null)
    where TEntity : IEntity;

public enum DeleteImageStatus
{
    Deleted,
    EntityNotFound,
    NoImage,
}

/// <summary>
/// Orchestrates entity writes (normalization, cover image storage/replacement/cleanup) so the endpoints stay thin.
/// Knows nothing about HTTP results.
/// </summary>
public interface IEntityWriteService<TEntity> where TEntity : IEntity
{
    Task<EntityWriteResult<TEntity>> CreateAsync(TEntity entity, ImageInput image, CancellationToken cancellationToken);

    Task<EntityWriteResult<TEntity>> UpdateAsync(string id, TEntity entity, ImageInput image, CancellationToken cancellationToken);

    /// <summary>Deletes the entity and its cover image file (a file that cannot be deleted never fails the delete).</summary>
    Task<DeleteResult> DeleteAsync(string id, CancellationToken cancellationToken);

    /// <summary>Deletes only the cover image: the file, then the reference on the entity.</summary>
    Task<DeleteImageStatus> DeleteImageAsync(string id, CancellationToken cancellationToken);
}
