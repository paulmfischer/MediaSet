using MediaSet.Api.Filters;
using MediaSet.Data.Entities;
using MediaSet.Data.Repositories;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MediaSet.Api.BookApi;

internal static class BookApi
{
    public static RouteGroupBuilder MapBooks(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/books");

        group.WithPrameterValidation(typeof(Book));
        group.WithPrameterValidation(typeof(CreateBook));
        group.WithPrameterValidation(typeof(UpdateBook));

        group.MapGet("/", (IBookRepository bookRepository) =>
        {
            return bookRepository.GetAllBooks();
        });

        group.MapGet("/{id}", async Task<Results<Ok<Book>, NotFound>> (IBookRepository bookRepository, int id) =>
        {
            return await bookRepository.GetBookById(id) switch 
            {
                Book book => TypedResults.Ok(book),
                _ => TypedResults.NotFound()
            };
        });

        group.MapPost("/", async Task<Created<Book>> (IBookRepository bookRepository, CreateBook createBook) =>
        {
            var book = await bookRepository.CreateBook(createBook.AsBook());
            return TypedResults.Created($"/books/{book.Id}", book);
        });

        group.MapPut("/{id}", async Task<Results<Ok<Book>, NotFound, BadRequest<string>>> (IBookRepository bookRepository, int id, UpdateBook book) =>
        {
            if (id != book.Id)
            {
                return TypedResults.BadRequest("Book Id and route id do not match");
            }
            var updatedBook = await bookRepository.UpdateBook(book.AsBook());

            return updatedBook is null ? TypedResults.NotFound() : TypedResults.Ok(updatedBook);
        });

        group.MapDelete("/{id}", async Task<Results<NotFound, Ok>> (IBookRepository bookRepository, int id) =>
        {
            var rowsAffected = await bookRepository.DeleteBookById(id);
            
            return rowsAffected == 0 ? TypedResults.NotFound() : TypedResults.Ok();
        });

        return group;
    }
}