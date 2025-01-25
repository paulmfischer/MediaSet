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
    var entityName = $"{typeof(TEntity).Name}s";
    var group = routes.MapGroup($"/{entityName}");

    group.WithTags(entityName);

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
        return TypedResults.BadRequest();
      }

      await entityService.CreateAsync(newEntity);

      return TypedResults.Created($"/{typeof(TEntity).Name}/{newEntity.Id}", newEntity);
    });

    group.MapPut("/{id}", async Task<Results<Ok, NotFound, BadRequest>> (EntityService<TEntity> entityService, string id, TEntity updatedEntity) =>
    {
      if (id != updatedEntity.Id)
      {
        return TypedResults.BadRequest();
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
      Console.WriteLine("File upload received: {0}!", bookUpload.FileName);
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
            if (headerFields != null)
            {
              // Console.WriteLine("Header Fields: {0}", string.Join(',', headerFields));
            }
            else
            {
              Console.WriteLine("No header fields in upload document");
              return TypedResults.BadRequest("No header fields in upload document.");
            }
          }
          else
          {
            // process data rows
            string[]? dataRow = parser.ReadFields();
            if (dataRow != null)
            {
              // Console.WriteLine("Entity Data: {0}", string.Join(',', dataRow));
              dataFields.Add(dataRow);
            }
          }
        }

        if (dataFields.Count == 0)
        {
          Console.WriteLine("No header fields in upload document");
          return TypedResults.BadRequest("No header fields in upload document.");
        }

        newEntities = UploadService.MapUploadToEntities<TEntity>(headerFields, dataFields);
        await entityService.BulkCreateAsync(newEntities);
      }
      catch (Exception er)
      {
        Console.WriteLine("Failed to save bulk create: {0}", er);
        return TypedResults.BadRequest(string.Format("Failed to save bulk create: {0}", er));
      }

      return TypedResults.Ok(string.Format("Uploaded {0} new {1}", newEntities.Count(), $"{typeof(TEntity).Name}s"));
    })
    .DisableAntiforgery();

    return group;
  }
}