using MediaSet.Api.Features.Entities.Models;
using MediaSet.Api.Features.Entities.Services;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Storage;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.VisualBasic.FileIO;

namespace MediaSet.Api.Features.Entities.Endpoints;

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
                IFormCollection? multipartForm = null;

                // Check if request is multipart form data or JSON
                if (context.Request.ContentType?.Contains("multipart/form-data") == true)
                {
                    // Parse multipart form data once
                    multipartForm = await context.Request.ReadFormAsync(cancellationToken);
                    
                    // Deserialize entity from JSON
                    if (!multipartForm.TryGetValue("entity", out var entityJson) || entityJson.Count == 0)
                    {
                        logger.LogError("Entity JSON not provided in form data for {entityType}", entityType);
                        return TypedResults.BadRequest("Entity JSON is required in form data");
                    }

                    var entityJsonString = entityJson[0]!;
                    var options = new System.Text.Json.JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true,
                        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                    };
                    var deserializedEntity = System.Text.Json.JsonSerializer.Deserialize<TEntity>(entityJsonString, options);
                    if (deserializedEntity is null)
                    {
                        logger.LogError("Failed to deserialize entity for {entityType}", entityType);
                        return TypedResults.BadRequest("Failed to deserialize entity");
                    }

                    newEntity = deserializedEntity;
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
                }

                if (newEntity.IsEmpty())
                {
                    logger.LogError("A {entity} is required and must have some data on it", typeof(TEntity).Name);
                    return TypedResults.BadRequest("Entity is required and must have some data");
                }

                logger.LogInformation("Creating new {entityType}", entityType);
                await entityService.CreateAsync(newEntity, cancellationToken);
                logger.LogInformation("Created {entityType} with id {id}", entityType, newEntity.Id);

                // Process image after entity is saved so we have an ID
                // Flow 1: Handle multipart/form-data with file upload and/or imageUrl
                if (multipartForm is not null)
                {
                    // Check for image file in multipart form
                    var imageFile = multipartForm.Files["coverImage"];
                    if (imageFile is not null && imageFile.Length > 0)
                    {
                        logger.LogInformation("Processing image file upload for {entityType}/{id}", entityType, newEntity.Id);

                        try
                        {
                            var image = await imageService.SaveImageAsync(imageFile, entityType.ToLower(), newEntity.Id!, cancellationToken);
                            newEntity.CoverImage = image;
                            // Update entity with image
                            await entityService.UpdateAsync(newEntity.Id!, newEntity, cancellationToken);
                            logger.LogInformation("Updated {entityType} {id} with cover image", entityType, newEntity.Id);
                        }
                        catch (ArgumentException ex)
                        {
                            logger.LogWarning("Image validation failed: {error}", ex.Message);
                            // Continue anyway - entity was already created successfully
                        }
                        
                        // Ensure imageUrl is not persisted when a file is uploaded (file takes precedence)
                        var imageUrlProp = newEntity.GetType().GetProperty("ImageUrl");
                        imageUrlProp?.SetValue(newEntity, null);
                        await entityService.UpdateAsync(newEntity.Id!, newEntity, cancellationToken);
                    }
                    else if (multipartForm.TryGetValue("imageUrl", out var imageUrlValue) && !string.IsNullOrWhiteSpace(imageUrlValue.ToString()))
                    {
                        // No file provided, check for imageUrl
                        var imageUrl = imageUrlValue.ToString();
                        logger.LogInformation("Processing image URL download for {entityType}/{id}: {url}", entityType, newEntity.Id, imageUrl);

                        try
                        {
                            var image = await imageService.DownloadAndSaveImageAsync(imageUrl, entityType, newEntity.Id!, cancellationToken);
                            newEntity.CoverImage = image;
                            // Update entity with image
                            await entityService.UpdateAsync(newEntity.Id!, newEntity, cancellationToken);
                            logger.LogInformation("Image downloaded and saved successfully for {entityType}/{id}", entityType, newEntity.Id);
                        }
                        catch (ArgumentException ex)
                        {
                            logger.LogWarning("Failed to download image from URL: {error}", ex.Message);
                            // Continue anyway - entity was already created successfully
                        }
                        catch (HttpRequestException ex)
                        {
                            logger.LogWarning("HTTP error downloading image: {error}", ex.Message);
                            // Continue anyway - entity was already created successfully
                        }
                    }
                }
                // Flow 2: Handle JSON update with imageUrl
                else
                {
                    // Check if imageUrl is provided for download
                    var imageUrlProp = newEntity.GetType().GetProperty("ImageUrl");
                    var imageUrl = imageUrlProp?.GetValue(newEntity) as string;
                    
                    if (!string.IsNullOrWhiteSpace(imageUrl))
                    {
                        logger.LogInformation("Processing image URL download for {entityType}/{id}: {url}", entityType, newEntity.Id, imageUrl);

                        try
                        {
                            var image = await imageService.DownloadAndSaveImageAsync(imageUrl, entityType, newEntity.Id!, cancellationToken);
                            newEntity.CoverImage = image;
                            // Update entity with image
                            await entityService.UpdateAsync(newEntity.Id!, newEntity, cancellationToken);
                            logger.LogInformation("Image downloaded and saved successfully for {entityType}/{id}", entityType, newEntity.Id);
                        }
                        catch (ArgumentException ex)
                        {
                            logger.LogWarning("Failed to download image from URL: {error}", ex.Message);
                            // Continue anyway - entity was already created successfully
                        }
                        catch (HttpRequestException ex)
                        {
                            logger.LogWarning("HTTP error downloading image: {error}", ex.Message);
                            // Continue anyway - entity was already created successfully
                        }
                    }
                }

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
                IFormCollection? multipartForm = null;

                // Check if request is multipart form data or JSON
                if (context.Request.ContentType?.Contains("multipart/form-data") == true)
                {
                    // Parse multipart form data once
                    multipartForm = await context.Request.ReadFormAsync(cancellationToken);
                    
                    // Deserialize entity from JSON
                    if (!multipartForm.TryGetValue("entity", out var entityJson) || entityJson.Count == 0)
                    {
                        logger.LogError("Entity JSON not provided in form data for {entityType}/{id}", entityType, id);
                        return TypedResults.BadRequest("Entity JSON is required in form data");
                    }

                    var entityJsonString = entityJson[0]!;
                    var options = new System.Text.Json.JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true,
                        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                    };
                    var deserializedEntity = System.Text.Json.JsonSerializer.Deserialize<TEntity>(entityJsonString, options);
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

                // Flow 1: Handle multipart/form-data with file upload and/or imageUrl
                if (multipartForm is not null)
                {
                    // Check for image file in multipart form
                    var imageFile = multipartForm.Files["coverImage"];
                    if (imageFile is not null && imageFile.Length > 0)
                    {
                        logger.LogInformation("Processing image file upload for {entityType}/{id}", entityType, id);
                        
                        // Delete old image if it exists
                        DeleteOldImage(existingEntity, imageService, logger);

                        try
                        {
                            var image = await imageService.SaveImageAsync(imageFile, entityType.ToLower(), id, cancellationToken);
                            updatedEntity.CoverImage = image;
                            logger.LogInformation("Image file processed successfully for {entityType}/{id}", entityType, id);
                        }
                        catch (ArgumentException ex)
                        {
                            logger.LogWarning("Image validation failed: {error}", ex.Message);
                            // Continue anyway - entity update should succeed even if image fails
                        }
                        
                        // Ensure imageUrl is not persisted when a file is uploaded (file takes precedence)
                        var imageUrlProp = updatedEntity.GetType().GetProperty("ImageUrl");
                        imageUrlProp?.SetValue(updatedEntity, null);
                    }
                    else if (multipartForm.TryGetValue("imageUrl", out var imageUrlValue) && !string.IsNullOrWhiteSpace(imageUrlValue.ToString()))
                    {
                        // No file provided, check for imageUrl
                        var imageUrl = imageUrlValue.ToString();
                        logger.LogInformation("Processing image URL download for {entityType}/{id}: {url}", entityType, id, imageUrl);
                        
                        // Delete old image if it exists
                        DeleteOldImage(existingEntity, imageService, logger);

                        try
                        {
                            var image = await imageService.DownloadAndSaveImageAsync(imageUrl, entityType, id, cancellationToken);
                            updatedEntity.CoverImage = image;
                            logger.LogInformation("Image downloaded and saved successfully for {entityType}/{id}", entityType, id);
                        }
                        catch (ArgumentException ex)
                        {
                            logger.LogWarning("Failed to download image from URL: {error}", ex.Message);
                            // Continue anyway - entity update should succeed even if image fails
                        }
                        catch (HttpRequestException ex)
                        {
                            logger.LogWarning("HTTP error downloading image: {error}", ex.Message);
                            // Continue anyway - entity update should succeed even if image fails
                        }
                    }
                    else if (updatedEntity.CoverImage is null && existingEntity.CoverImage is not null)
                    {
                        // Neither file nor imageUrl provided, but old image exists - delete it
                        logger.LogInformation("Image is being cleared for {entityType}/{id}", entityType, id);
                        DeleteOldImage(existingEntity, imageService, logger);
                    }
                }
                // Flow 2: Handle JSON update with imageUrl
                else
                {
                    // Check if imageUrl is provided for download
                    var imageUrlProp = updatedEntity.GetType().GetProperty("ImageUrl");
                    var imageUrl = imageUrlProp?.GetValue(updatedEntity) as string;
                    var existingImageUrl = imageUrlProp?.GetValue(existingEntity) as string;
                    
                    if (!string.IsNullOrWhiteSpace(imageUrl) && imageUrl != existingImageUrl)
                    {
                        logger.LogInformation("Processing image URL download for {entityType}/{id}: {url}", entityType, id, imageUrl);
                        
                        // Delete old image if it exists
                        DeleteOldImage(existingEntity, imageService, logger);

                        try
                        {
                            var image = await imageService.DownloadAndSaveImageAsync(imageUrl, entityType, id, cancellationToken);
                            updatedEntity.CoverImage = image;
                            logger.LogInformation("Image downloaded and saved successfully for {entityType}/{id}", entityType, id);
                        }
                        catch (ArgumentException ex)
                        {
                            logger.LogWarning("Failed to download image from URL: {error}", ex.Message);
                            // Continue anyway - entity update should succeed even if image fails
                        }
                        catch (HttpRequestException ex)
                        {
                            logger.LogWarning("HTTP error downloading image: {error}", ex.Message);
                            // Continue anyway - entity update should succeed even if image fails
                        }
                    }
                    // Check if image is being cleared (updatedEntity.CoverImage is null but existing had one)
                    else if (updatedEntity.CoverImage is null && existingEntity.CoverImage is not null)
                    {
                        logger.LogInformation("Image is being cleared for {entityType}/{id}", entityType, id);
                        // Delete the old image to prevent orphaned files
                        DeleteOldImage(existingEntity, imageService, logger);
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

        group.MapDelete("/{id}/image", async Task<Results<NoContent, NotFound>> (
            IEntityService<TEntity> entityService,
            [Microsoft.AspNetCore.Mvc.FromServices] IImageService imageService,
            string id,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation("Deleting image for {entityType}/{id}", entityType, id);

            // Get the entity
            var entity = await entityService.GetAsync(id, cancellationToken);
            if (entity is null)
            {
                logger.LogWarning("{entityType} not found for id {id}", entityType, id);
                return TypedResults.NotFound();
            }

            if (entity.CoverImage is null)
            {
                logger.LogWarning("No image found for {entityType}/{id}", entityType, id);
                return TypedResults.NotFound();
            }

            // Delete the image file
            try
            {
                imageService.DeleteImageAsync(entity.CoverImage.FilePath);
                logger.LogInformation("Deleted image file: {imagePath}", entity.CoverImage.FilePath);
            }
            catch (Exception ex)
            {
                logger.LogWarning("Failed to delete image file: {error}", ex.Message);
                // Continue - file might already be deleted
            }

            // Remove coverImage from entity
            entity.CoverImage = null;
            await entityService.UpdateAsync(id, entity, cancellationToken);
            logger.LogInformation("Removed image reference from {entityType}/{id}", entityType, id);

            return TypedResults.NoContent();
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
                    var image = await imageService.DownloadAndSaveImageAsync(imageUrl, entityType, entityId, cancellationToken);
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
