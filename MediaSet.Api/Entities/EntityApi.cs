using MediaSet.Api.Clients;
using MediaSet.Api.Helpers;
using MediaSet.Api.Models;
using MediaSet.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.VisualBasic.FileIO;

namespace MediaSet.Api.Entities;

internal static class EntityApi
{
    public static RouteGroupBuilder MapEntity<TEntity>(this IEndpointRouteBuilder routes) where TEntity : IEntity, new()
    {
        // get the app.Logger to be used for logging.
        var logger = ((WebApplication)routes).Logger;
        var entityType = $"{typeof(TEntity).Name}s";
        var group = routes.MapGroup($"/{entityType}");

        group.WithTags(entityType);

        group.MapGet("/", async (IEntityService<TEntity> entityService, CancellationToken cancellationToken) =>
        {
            logger.LogInformation("Listing all {entityType}", entityType);
            var result = await entityService.GetListAsync(cancellationToken);
            logger.LogInformation("Retrieved {count} {entityType}", result.Count(), entityType);
            return result;
        });

        group.MapGet("/search", async (IEntityService<TEntity> entityService, string searchText, string orderBy, CancellationToken cancellationToken) =>
        {
            logger.LogInformation("Searching {entityType} with query '{searchText}', orderBy {orderBy}", entityType, searchText, orderBy);
            var result = await entityService.SearchAsync(searchText, orderBy, cancellationToken);
            logger.LogInformation("Search returned {count} {entityType}", result.Count(), entityType);
            return result;
        });

        group.MapGet("/{id}", async Task<Results<Ok<TEntity>, NotFound>> (IEntityService<TEntity> entityService, string id, CancellationToken cancellationToken) =>
        {
            logger.LogInformation("Fetching {entityType} by id {id}", entityType, id);
            var entity = await entityService.GetAsync(id, cancellationToken);
            if (entity is not null)
            {
                return TypedResults.Ok(entity);
            }

            logger.LogWarning("{entityType} not found for id {id}", entityType, id);
            return TypedResults.NotFound();
        });

        group.MapPost("/", async Task<Results<Created<TEntity>, BadRequest<string>>> (
            IEntityService<TEntity> entityService,
            [Microsoft.AspNetCore.Mvc.FromServices] IImageService imageService,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            try
            {
                TEntity newEntity;

                // Check if request is multipart form data or JSON
                if (context.Request.ContentType?.Contains("multipart/form-data") == true)
                {
                    // Parse multipart form data
                    var form = await context.Request.ReadFormAsync(cancellationToken);
                    
                    // Deserialize entity from JSON
                    if (!form.TryGetValue("entity", out var entityJson))
                    {
                        logger.LogError("Entity JSON not provided in form data for {entityType}", entityType);
                        return TypedResults.BadRequest("Entity JSON is required in form data");
                    }

                    var deserializedEntity = System.Text.Json.JsonSerializer.Deserialize<TEntity>(entityJson.ToString());
                    if (deserializedEntity is null)
                    {
                        logger.LogError("Failed to deserialize entity for {entityType}", entityType);
                        return TypedResults.BadRequest("Failed to deserialize entity");
                    }

                    newEntity = deserializedEntity;
                    
                    // Generate ID if not provided
                    if (string.IsNullOrEmpty(newEntity.Id))
                    {
                        newEntity.Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
                    }

                    // Handle image upload or download
                    try
                    {
                        if (form.Files.Count > 0 || form.Keys.Contains("imageUrl"))
                        {
                            var (success, errorMessage, image) = await TryProcessImageAsync(
                                form,
                                imageService,
                                null, // No existing entity on POST
                                entityType,
                                newEntity.Id!,
                                logger,
                                cancellationToken);

                            if (!success && errorMessage is not null)
                            {
                                return TypedResults.BadRequest(errorMessage);
                            }

                            if (image is not null)
                            {
                                newEntity.CoverImage = image;
                            }
                        }
                    }
                    catch (ArgumentException ex)
                    {
                        logger.LogWarning("Image validation failed: {error}", ex.Message);
                        return TypedResults.BadRequest($"Image validation failed: {ex.Message}");
                    }
                }
                else
                {
                    // For backward compatibility, try to read as JSON from body
                    var deserializedEntity = await context.Request.ReadFromJsonAsync<TEntity>(cancellationToken);
                    if (deserializedEntity is null)
                    {
                        logger.LogError("Failed to deserialize entity from request body for {entityType}", entityType);
                        return TypedResults.BadRequest("Entity is required and must have some data");
                    }
                    
                    newEntity = deserializedEntity;
                    
                    // Generate ID if not provided
                    if (string.IsNullOrEmpty(newEntity.Id))
                    {
                        newEntity.Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
                    }
                }

                if (newEntity.IsEmpty())
                {
                    logger.LogError("A {entity} is required and must have some data on it", typeof(TEntity).Name);
                    return TypedResults.BadRequest("Entity is required and must have some data");
                }

                logger.LogInformation("Creating new {entityType}", entityType);
                await entityService.CreateAsync(newEntity, cancellationToken);
                logger.LogInformation("Created {entityType} with id {id}", entityType, newEntity.Id);

                return TypedResults.Created($"/{typeof(TEntity).Name}/{newEntity.Id}", newEntity);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating {entityType}", entityType);
                return TypedResults.BadRequest($"Error creating entity: {ex.Message}");
            }
        }).DisableAntiforgery();

        group.MapPut("/{id}", async Task<Results<Ok, NotFound, BadRequest<string>>> (
            IEntityService<TEntity> entityService,
            [Microsoft.AspNetCore.Mvc.FromServices] IImageService imageService,
            string id,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            try
            {
                TEntity updatedEntity;

                // Check if request is multipart form data or JSON
                if (context.Request.ContentType?.Contains("multipart/form-data") == true)
                {
                    // Parse multipart form data
                    var form = await context.Request.ReadFormAsync(cancellationToken);
                    
                    // Deserialize entity from JSON
                    if (!form.TryGetValue("entity", out var entityJson))
                    {
                        logger.LogError("Entity JSON not provided in form data for {entityType}/{id}", entityType, id);
                        return TypedResults.BadRequest("Entity JSON is required in form data");
                    }

                    var deserializedEntity = System.Text.Json.JsonSerializer.Deserialize<TEntity>(entityJson.ToString());
                    if (deserializedEntity is null)
                    {
                        logger.LogError("Failed to deserialize entity for {entityType}/{id}", entityType, id);
                        return TypedResults.BadRequest("Failed to deserialize entity");
                    }

                    updatedEntity = deserializedEntity;
                }
                else
                {
                    // For backward compatibility, try to read as JSON from body
                    var deserializedEntity = await context.Request.ReadFromJsonAsync<TEntity>(cancellationToken);
                    if (deserializedEntity is null)
                    {
                        logger.LogError("Failed to deserialize entity from request body for {entityType}/{id}", entityType, id);
                        return TypedResults.BadRequest("Failed to deserialize entity");
                    }

                    updatedEntity = deserializedEntity;
                }

                if (id != updatedEntity.Id)
                {
                    logger.LogError("Ids on the entity and the request do not match: {pathId} != {entityId}", id, updatedEntity.Id);
                    return TypedResults.BadRequest("Ids on the entity and the request do not match");
                }

                // Get the existing entity to access old image for cleanup
                var existingEntity = await entityService.GetAsync(id, cancellationToken);
                if (existingEntity is null)
                {
                    logger.LogWarning("{entityType} not found for id {id}", entityType, id);
                    return TypedResults.NotFound();
                }

                // Handle image upload or download (only if multipart form data)
                if (context.Request.ContentType?.Contains("multipart/form-data") == true)
                {
                    var form = await context.Request.ReadFormAsync(cancellationToken);
                    
                    if (form.Files.Count > 0 || form.Keys.Contains("imageUrl"))
                    {
                        var (success, errorMessage, image) = await TryProcessImageAsync(
                            form,
                            imageService,
                            existingEntity,
                            entityType,
                            id,
                            logger,
                            cancellationToken);

                        if (!success && errorMessage is not null)
                        {
                            return TypedResults.BadRequest(errorMessage);
                        }

                        if (image is not null)
                        {
                            updatedEntity.CoverImage = image;
                        }
                    }
                }

                var result = await entityService.UpdateAsync(id, updatedEntity, cancellationToken);
                logger.LogInformation("Updated {entityType} {entityId}: {updated}", entityType, id, result.ModifiedCount > 0);
                return TypedResults.Ok();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating {entityType}/{id}", entityType, id);
                return TypedResults.BadRequest($"Error updating entity: {ex.Message}");
            }
        }).DisableAntiforgery();

        group.MapDelete("/{id}", async Task<Results<NotFound, Ok>> (
            IEntityService<TEntity> entityService,
            [Microsoft.AspNetCore.Mvc.FromServices] IImageService imageService,
            string id,
            CancellationToken cancellationToken) =>
        {
            // Get entity to retrieve image path for cleanup
            var entity = await entityService.GetAsync(id, cancellationToken);
            if (entity is not null && entity.CoverImage is not null)
            {
                try
                {
                    imageService.DeleteImageAsync(entity.CoverImage.FilePath);
                    logger.LogInformation("Deleted image during entity deletion: {imagePath}", entity.CoverImage.FilePath);
                }
                catch (IOException ex)
                {
                    logger.LogWarning("Failed to delete image during entity deletion (IO error): {error}", ex.Message);
                    // Continue anyway - don't fail the entity deletion
                }
                catch (UnauthorizedAccessException ex)
                {
                    logger.LogWarning("Failed to delete image during entity deletion (unauthorized): {error}", ex.Message);
                    // Continue anyway - don't fail the entity deletion
                }
            }

            var result = await entityService.RemoveAsync(id, cancellationToken);
            logger.LogInformation("Deleted {entityType} {entityId}: {deleted}", entityType, id, result.DeletedCount > 0);
            return TypedResults.Ok();
        });

        group.MapPost("/upload", async Task<Results<Ok<string>, BadRequest<string>>> (IEntityService<TEntity> entityService, IFormFile bookUpload, CancellationToken cancellationToken) =>
       {
           logger.LogInformation("Received {fileName} file to upload to {entityType}", bookUpload.FileName, entityType);
           IEnumerable<TEntity> newEntities;
           try
           {
               using Stream stream = bookUpload.OpenReadStream();

               using TextFieldParser parser = new(stream, System.Text.Encoding.UTF8);
               parser.TextFieldType = FieldType.Delimited;
               parser.SetDelimiters(";");
               parser.HasFieldsEnclosedInQuotes = true;
               IList<string>? headerFields = [];
               IList<string[]> dataFields = [];
               while (!parser.EndOfData)
               {
                   //Process header row
                   if (parser.LineNumber == 1)
                   {
                       headerFields = parser.ReadFields();
                       if (headerFields == null)
                       {
                           logger.LogError("No header fields are included in upload document");
                           return TypedResults.BadRequest("No header fields in upload document.");
                       }
                       logger.LogDebug("Header Fields: {headerFields}", string.Join(',', headerFields));
                   }
                   else
                   {
                       // process data rows
                       string[]? dataRow = parser.ReadFields();
                       if (dataRow != null)
                       {
                           logger.LogTrace("Entity Data: {dataRow}", string.Join(',', dataRow));
                           dataFields.Add(dataRow);
                       }
                   }
               }

               if (dataFields.Count == 0)
               {
                   logger.LogError("No data to upload");
                   return TypedResults.BadRequest("No data to upload.");
               }

               newEntities = UploadService.MapUploadToEntities<TEntity>(headerFields, dataFields);
               logger.LogInformation("Parsed {count} {entityType} from upload", newEntities.Count(), entityType);
               await entityService.BulkCreateAsync(newEntities, cancellationToken);
           }
           catch (Exception er)
           {
               logger.LogError(er, "Failed to save bulk create for {entityType}", entityType);
               return TypedResults.BadRequest(string.Format("Failed to save bulk create: {0}", er));
           }

           logger.LogInformation("Uploaded {count} new {entityType}", newEntities.Count(), entityType);
           return TypedResults.Ok(string.Format("Uploaded {0} new {1}", newEntities.Count(), entityType));
       })
       .DisableAntiforgery();

        return group;
    }

    /// <summary>
    /// Helper method to process image upload or download from a multipart form.
    /// </summary>
    /// <returns>Tuple of (success, errorMessage, processedImage)</returns>
    private static async Task<(bool Success, string? ErrorMessage, Image? ProcessedImage)> TryProcessImageAsync(
        IFormCollection form,
        IImageService imageService,
        IEntity? existingEntity,
        string entityType,
        string entityId,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            // Check for image file in multipart form
            var imageFile = form.Files["coverImage"];
            if (imageFile is not null && imageFile.Length > 0)
            {
                logger.LogInformation("Processing image file upload for {entityType}/{id}", entityType, entityId);
                
                // Delete old image if it exists
                DeleteOldImage(existingEntity, imageService, logger);

                var image = await imageService.SaveImageAsync(imageFile, entityType.ToLower(), entityId, cancellationToken);
                return (true, null, image);
            }
            // Check for imageUrl field if no file was provided
            else if (form.TryGetValue("imageUrl", out var imageUrlValue) && !string.IsNullOrWhiteSpace(imageUrlValue.ToString()))
            {
                var imageUrl = imageUrlValue.ToString();
                logger.LogInformation("Processing image URL download for {entityType}/{id}: {url}", entityType, entityId, imageUrl);
                
                // Delete old image if it exists
                DeleteOldImage(existingEntity, imageService, logger);

                try
                {
                    var image = await imageService.DownloadAndSaveImageAsync(imageUrl, entityType.ToLower(), entityId, cancellationToken);
                    return (true, null, image);
                }
                catch (ArgumentException ex)
                {
                    logger.LogWarning("Failed to download image from URL: {error}", ex.Message);
                    return (false, $"Failed to download image: {ex.Message}", null);
                }
                catch (HttpRequestException ex)
                {
                    logger.LogWarning("HTTP error downloading image: {error}", ex.Message);
                    return (false, $"Failed to download image from URL: {ex.Message}", null);
                }
            }

            return (true, null, null);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning("Image validation failed: {error}", ex.Message);
            return (false, $"Image validation failed: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Helper method to delete an old image with proper exception handling.
    /// </summary>
    private static void DeleteOldImage(IEntity? entity, IImageService imageService, ILogger logger)
    {
        if (entity?.CoverImage is null)
        {
            return;
        }

        try
        {
            imageService.DeleteImageAsync(entity.CoverImage.FilePath);
            logger.LogInformation("Deleted old image: {imagePath}", entity.CoverImage.FilePath);
        }
        catch (IOException ex)
        {
            logger.LogWarning("Failed to delete old image (IO error): {error}", ex.Message);
            // Continue anyway - don't fail the operation
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning("Failed to delete old image (unauthorized): {error}", ex.Message);
            // Continue anyway - don't fail the operation
        }
    }
}
