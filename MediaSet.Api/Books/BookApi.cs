using MediaSet.Api.Helpers;
using MediaSet.Api.Models;
using MediaSet.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.VisualBasic.FileIO;

namespace MediaSet.Api.Books;

internal static class BookApi
{
  public static RouteGroupBuilder MapBooks(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/books");

    group.WithTags("Books");

    group.MapGet("/", async (BooksService booksService) => await booksService.GetAsync()).WithOpenApi();
    group.MapGet("/search", async (BooksService bookService, string searchText, string orderBy) => await bookService.SearchAsync(searchText, orderBy));

    group.MapGet("/{id}", async Task<Results<Ok<Book>, NotFound>> (BooksService booksService, string id) =>
    {
      return await booksService.GetAsync(id) switch
      {
        Book book => TypedResults.Ok(book),
        _ => TypedResults.NotFound()
      };
    }).WithOpenApi();

    group.MapPost("/", async Task<Results<Created<Book>, BadRequest>> (BooksService booksService, Book newBook) =>
    {
      if (newBook is null || newBook.IsEmpty())
      {
        return TypedResults.BadRequest();
      }

      await booksService.CreateAsync(newBook);

      return TypedResults.Created($"/books/{newBook.Id}", newBook);
    }).WithOpenApi();

    group.MapPut("/{id}", async Task<Results<Ok, NotFound, BadRequest>> (BooksService booksService, string id, Book updatedBook) =>
    {
      if (id != updatedBook.Id)
      {
        return TypedResults.BadRequest();
      }

      var result = await booksService.UpdateAsync(id, updatedBook);
      return result.ModifiedCount == 0 ? TypedResults.NotFound() : TypedResults.Ok();
    });

    group.MapDelete("/{id}", async Task<Results<NotFound, Ok>> (BooksService booksService, string id) =>
    {
      var result = await booksService.RemoveAsync(id);
      return result.DeletedCount == 0 ? TypedResults.NotFound() : TypedResults.Ok();
    });

    group.MapPost("/upload", async Task<Results<Ok, BadRequest>> (BooksService booksService, IFormFile bookUpload) =>
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
            Console.WriteLine("What are the headers?");
            headerFields = parser.ReadFields();
            if (headerFields != null)
            {
              foreach (string field in headerFields)
              {
                Console.WriteLine("Header fields: {0}", field);
              }
            }
          }
          else
          {
            // process data rows
            string[]? newBookFields = parser.ReadFields();
            if (newBookFields != null)
            {
              foreach (string field in newBookFields)
              {
                Console.WriteLine("book fields: {0}", field);
              }
              var authors = newBookFields.GetByHeader(headerFields, nameof(Book.Author));
              var publishers = newBookFields.GetByHeader(headerFields, nameof(Book.Publisher));
              var genres = newBookFields.GetByHeader(headerFields, nameof(Book.Genre));
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
                Author = string.IsNullOrWhiteSpace(authors) ? [] : [.. authors.Split("|")],
                Publisher = string.IsNullOrWhiteSpace(publishers) ? [] : [.. publishers.Split("|")],
                Genre = string.IsNullOrWhiteSpace(genres) ? [] : [.. genres.Split("|")],
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
      catch (System.Exception er)
      {
        Console.WriteLine("Failed to save bulk create: {0}", er);
        return TypedResults.BadRequest();
      }

      return TypedResults.Ok();
    })
    .DisableAntiforgery();

    return group;
  }
}