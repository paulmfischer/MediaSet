using MediaSet.Api.Helpers;
using MediaSet.Api.Models;
using MediaSet.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.VisualBasic.FileIO;

namespace MediaSet.Api.Books;

internal static class EntityApi
{
  public static RouteGroupBuilder MapEntity<TEntity>(this IEndpointRouteBuilder routes) where TEntity : IEntity, new()
  {
    // get the app.Logger to be used for logging.
    var logger = ((WebApplication)routes).Logger;
    var entityType = $"{typeof(TEntity).Name}s";
    var group = routes.MapGroup($"/{entityType}");

    group.WithTags(entityType);

    group.MapGet("/", async (EntityService<TEntity> entityService) => await entityService.GetListAsync());
    group.MapGet("/search", async (EntityService<TEntity> entityService, string searchText, string orderBy) => await entityService.SearchAsync(searchText, orderBy));

    group.MapGet("/{id}", async Task<Results<Ok<TEntity>, NotFound>> (EntityService<TEntity> entityService, string id) =>
    {
      return await entityService.GetAsync(id) switch
      {
        TEntity entity => TypedResults.Ok(entity),
        _ => TypedResults.NotFound()
      };
    });

    group.MapPost("/", async Task<Results<Created<TEntity>, BadRequest>> (EntityService<TEntity> entityService, TEntity newEntity) =>
    {
      if (newEntity is null || newEntity.IsEmpty())
      {
        logger.LogError("A {entity} is required and must have some data on it", typeof(TEntity).Name);
        return TypedResults.BadRequest();
      }

      await entityService.CreateAsync(newEntity);

      return TypedResults.Created($"/{typeof(TEntity).Name}/{newEntity.Id}", newEntity);
    });

    group.MapPut("/{id}", async Task<Results<Ok, NotFound, BadRequest<string>>> (EntityService<TEntity> entityService, string id, TEntity updatedEntity) =>
    {
      if (id != updatedEntity.Id)
      {
        logger.LogError("Ids on the entity and the request do not match: {pathId} != {entityId}", id, updatedEntity.Id);
        return TypedResults.BadRequest("Ids on the entity and the request do not match");
      }

      var result = await entityService.UpdateAsync(id, updatedEntity);
      return result.ModifiedCount == 0 ? TypedResults.NotFound() : TypedResults.Ok();
    });

    group.MapDelete("/{id}", async Task<Results<NotFound, Ok>> (EntityService<TEntity> entityService, string id) =>
    {
      var result = await entityService.RemoveAsync(id);
      return result.DeletedCount == 0 ? TypedResults.NotFound() : TypedResults.Ok();
    });

     group.MapPost("/upload", async Task<Results<Ok<string>, BadRequest<string>>> (EntityService<TEntity> entityService, IFormFile bookUpload) =>
    {
      logger.LogInformation("Received {fileName} file to upload to {entity}s", bookUpload.FileName, typeof(TEntity).Name);
      IEnumerable<TEntity> newEntities;
      try
      {
        var entityType = typeof(TEntity);
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
        await entityService.BulkCreateAsync(newEntities);
      }
      catch (Exception er)
      {
        logger.LogError(er, "Failed to save bulk create for {entity}s", typeof(TEntity).Name);
        return TypedResults.BadRequest(string.Format("Failed to save bulk create: {0}", er));
      }

      logger.LogInformation("Uploaded {count} new {entity}s", newEntities.Count(), typeof(TEntity).Name);
      return TypedResults.Ok(string.Format("Uploaded {0} new {1}s", newEntities.Count(), typeof(TEntity).Name));
    })
    .DisableAntiforgery();

    return group;
  }
}