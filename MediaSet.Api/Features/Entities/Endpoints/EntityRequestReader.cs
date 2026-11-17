using System.Text.Json;
using System.Text.Json.Serialization;
using MediaSet.Api.Features.Entities.Services;
using MediaSet.Api.Shared.Models;

namespace MediaSet.Api.Features.Entities.Endpoints;

/// <summary>An entity read from a request together with its cover image input, or the message to answer 400 with.</summary>
internal readonly record struct EntityRequest<TEntity>(TEntity? Entity, ImageInput Image, string? Error) where TEntity : IEntity
{
    public static EntityRequest<TEntity> Fail(string error) => new(default, ImageInput.None, error);
}

/// <summary>Parses a create or update request (multipart form or JSON body) into an entity and an <see cref="ImageInput"/>.</summary>
internal static class EntityRequestReader
{
    private const string CoverImageField = "coverImage";
    private const string ImageUrlField = "imageUrl";
    private const string EntityField = "entity";

    private static readonly JsonSerializerOptions _formEntityOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <param name="request">The HTTP request.</param>
    /// <param name="emptyBodyMessage">The 400 message when a JSON body has no entity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<EntityRequest<TEntity>> ReadAsync<TEntity>(
        HttpRequest request, string emptyBodyMessage, CancellationToken cancellationToken) where TEntity : IEntity
    {
        if (request.ContentType?.Contains("multipart/form-data") == true)
        {
            var form = await request.ReadFormAsync(cancellationToken);

            if (!form.TryGetValue(EntityField, out var entityJson) || entityJson.Count == 0)
            {
                return EntityRequest<TEntity>.Fail("Entity JSON is required in form data");
            }

            var entity = JsonSerializer.Deserialize<TEntity>(entityJson[0]!, _formEntityOptions);
            if (entity is null)
            {
                return EntityRequest<TEntity>.Fail("Failed to deserialize entity");
            }

            return new(entity, ReadFormImage(form), null);
        }

        // For backward compatibility, a plain JSON body is accepted; its imageUrl is the image to download
        var body = await request.ReadFromJsonAsync<TEntity>(cancellationToken);
        return body is null
            ? EntityRequest<TEntity>.Fail(emptyBodyMessage)
            : new(body, ImageInput.FromEntityUrl(body.ImageUrl), null);
    }

    private static ImageInput ReadFormImage(IFormCollection form)
    {
        var file = form.Files[CoverImageField];
        if (file is not null && file.Length > 0)
        {
            return ImageInput.Upload(file);
        }

        var url = form.TryGetValue(ImageUrlField, out var value) ? value.ToString() : null;
        return string.IsNullOrWhiteSpace(url) ? ImageInput.None : ImageInput.Download(url);
    }
}
