using MediaSet.Api.Models;
using MediaSet.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MediaSet.Api.Books;

internal static class BookApi
{
  public static RouteGroupBuilder MapBooks(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/books");

    group.WithTags("Books");

    group.MapGet("/", async (BooksService booksService) => await booksService.GetAsync()).WithOpenApi();

    group.MapGet("/{id}", async Task<Results<Ok<Book>, NotFound>> (BooksService booksService, string id) =>
    {
      return await booksService.GetAsync(id) switch
      {
        Book book => TypedResults.Ok(book),
        _ => TypedResults.NotFound()
      };
    }).WithOpenApi();

    group.MapPost("/", async Task<Created<Book>> (BooksService booksService, Book newBook) =>
    {
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

    return group;
  }
}