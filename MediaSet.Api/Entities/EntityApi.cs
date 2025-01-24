using MediaSet.Api.Helpers;
using MediaSet.Api.Models;
using MediaSet.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MediaSet.Api.Books;

internal static class EntityApi
{
  public static RouteGroupBuilder MapEntity<TEntity>(this IEndpointRouteBuilder routes) where TEntity : IEntity
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

    /* group.MapPost("/upload", async Task<Results<Ok, BadRequest>> (BookService booksService, IFormFile bookUpload) =>
    {
      Console.WriteLine("File upload received: {0}!", bookUpload.FileName);
      try
      {
        
        using Stream stream = bookUpload.OpenReadStream();

        using TextFieldParser parser = new(stream, System.Text.Encoding.UTF8);
        parser.TextFieldType = FieldType.Delimited;
        parser.SetDelimiters(";");
        parser.HasFieldsEnclosedInQuotes = true;
        string[]? headerFields = [];
        IList<Book> newBooks = [];
        while (!parser.EndOfData)
        {
          //Process header row
          if (parser.LineNumber == 1)
          {
            headerFields = parser.ReadFields();
            // if (headerFields != null)
            // {
            //   foreach (string field in headerFields)
            //   {
            //     Console.WriteLine("Header fields: {0}", field);
            //   }
            // }
          }
          else
          {
            // process data rows
            string[]? newBookFields = parser.ReadFields();
            if (newBookFields != null)
            {
              // foreach (string field in newBookFields)
              // {
              //   Console.WriteLine("book fields: {0}", field);
              // }
              var authors = newBookFields.GetByHeader(headerFields, "Author");
              var publisher = newBookFields.GetByHeader(headerFields, nameof(Book.Publisher));
              var genres = newBookFields.GetByHeader(headerFields, "Genre");
              var pagesField = newBookFields.GetByHeader(headerFields, nameof(Book.Pages));
              var publicationDateField = newBookFields.GetByHeader(headerFields, "Publication Date");
              // int.TryParse(newBookFields.GetByHeader(headerFields, nameof(Book.Pages)), out var pageCount);
              newBooks.Add(new()
              {
                Title = newBookFields.GetByHeader(headerFields, nameof(Book.Title)),
                ISBN = newBookFields.GetByHeader(headerFields, nameof(Book.ISBN)),
                Format = newBookFields.GetByHeader(headerFields, nameof(Book.Format)),
                Pages = string.IsNullOrWhiteSpace(pagesField) ? null : int.Parse(pagesField),
                PublicationDate = publicationDateField, // string.IsNullOrWhiteSpace(publicationDateField) ? null : DateTime.Parse(newBookFields.GetByHeader(headerFields, "Publication Date")),
                Authors = string.IsNullOrWhiteSpace(authors) ? [] : [.. authors.Split("|")],
                Publisher = newBookFields.GetByHeader(headerFields, nameof(Book.Publisher)),
                Genres = string.IsNullOrWhiteSpace(genres) ? [] : [.. genres.Split("|")],
                Plot = newBookFields.GetByHeader(headerFields, nameof(Book.Plot)),
                Subtitle = newBookFields.GetByHeader(headerFields, nameof(Book.Subtitle)),
              });
            }
          }
        }

        foreach (var book in newBooks)
        {
          await booksService.CreateAsync(book);
        }
      }
      catch (Exception er)
      {
        Console.WriteLine("Failed to save bulk create: {0}", er);
        return TypedResults.BadRequest();
      }

      return TypedResults.Ok();
    })
    .DisableAntiforgery(); */

    return group;
  }
}