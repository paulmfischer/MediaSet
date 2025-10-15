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

    group.MapGet("/", async (IEntityService<TEntity> entityService) =>
    {
      logger.LogInformation("Listing all {entityType}", entityType);
      var result = await entityService.GetListAsync();
      logger.LogInformation("Retrieved {count} {entityType}", result.Count(), entityType);
      return result;
    });

    group.MapGet("/search", async (IEntityService<TEntity> entityService, string searchText, string orderBy) =>
    {
      logger.LogInformation("Searching {entityType} with query '{searchText}', orderBy {orderBy}", entityType, searchText, orderBy);
      var result = await entityService.SearchAsync(searchText, orderBy);
      logger.LogInformation("Search returned {count} {entityType}", result.Count(), entityType);
      return result;
    });

    group.MapGet("/{id}", async Task<Results<Ok<TEntity>, NotFound>> (IEntityService<TEntity> entityService, string id) =>
    {
      logger.LogInformation("Fetching {entityType} by id {id}", entityType, id);
      var entity = await entityService.GetAsync(id);
      if (entity is not null)
      {
        return TypedResults.Ok(entity);
      }

      logger.LogWarning("{entityType} not found for id {id}", entityType, id);
      return TypedResults.NotFound();
    });

    group.MapPost("/", async Task<Results<Created<TEntity>, BadRequest>> (IEntityService<TEntity> entityService, TEntity newEntity) =>
    {
      if (newEntity is null || newEntity.IsEmpty())
      {
        logger.LogError("A {entity} is required and must have some data on it", typeof(TEntity).Name);
        return TypedResults.BadRequest();
      }

      logger.LogInformation("Creating new {entityType}", entityType);
      await entityService.CreateAsync(newEntity);
      logger.LogInformation("Created {entityType} with id {id}", entityType, newEntity.Id);

      return TypedResults.Created($"/{typeof(TEntity).Name}/{newEntity.Id}", newEntity);
    });

    group.MapPut("/{id}", async Task<Results<Ok, NotFound, BadRequest<string>>> (IEntityService<TEntity> entityService, string id, TEntity updatedEntity) =>
    {
      if (id != updatedEntity.Id)
      {
        logger.LogError("Ids on the entity and the request do not match: {pathId} != {entityId}", id, updatedEntity.Id);
        return TypedResults.BadRequest("Ids on the entity and the request do not match");
      }

      var result = await entityService.UpdateAsync(id, updatedEntity);
      logger.LogInformation("Updated {entityType} {entityId}: {updated}", entityType, id, result.ModifiedCount > 0);
      return TypedResults.Ok();
    });

    group.MapDelete("/{id}", async Task<Results<NotFound, Ok>> (IEntityService<TEntity> entityService, string id) =>
    {
      var result = await entityService.RemoveAsync(id);
      logger.LogInformation("Deleted {entityType} {entityId}: {deleted}", entityType, id, result.DeletedCount > 0);
      return TypedResults.Ok();
    });

    group.MapPost("/upload", async Task<Results<Ok<string>, BadRequest<string>>> (IEntityService<TEntity> entityService, IFormFile bookUpload) =>
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
       await entityService.BulkCreateAsync(newEntities);
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
}
