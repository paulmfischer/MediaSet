using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MediaSet.API;

namespace MediaSet.API.Books;

internal static class BookApi
{
    public static RouteGroupBuilder MapBooks(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/books");

        group.WithTags("Books");

        group.WithParameterValidation(typeof(BookItem));

        group.MapGet("/", async (MediaSetDbContext db) => 
        {
            return await db.Books.Select(book => book.AsBookItem()).AsNoTracking().ToListAsync();
        });

        group.MapGet("/{id}", async Task<Results<Ok<BookItem>, NotFound>> (MediaSetDbContext db, int id) =>
        {
            return await db.Books.FindAsync(id) switch
            {
                Book book => TypedResults.Ok(book.AsBookItem()),
                _ => TypedResults.NotFound()
            };
        });

        group.MapPost("/", async Task<Created<BookItem>> (MediaSetDbContext db, BookItem bookItem) =>
        {
            var book = new Book
            {
                ISBN = bookItem.ISBN,
                NumberOfPages = bookItem.NumberOfPages,
                PublishDate = bookItem.PublishDate,
                Title = bookItem.Title
            };

            db.Books.Add(book);
            await db.SaveChangesAsync();

            return TypedResults.Created($"/books/{book.Id}", book.AsBookItem());
        });

        group.MapPut("/{id}", async Task<Results<Ok, NotFound, BadRequest>> (MediaSetDbContext db, int id, BookItem bookItem) =>
        {
            if (id != bookItem.Id)
            {
                return TypedResults.BadRequest();
            }

            var rowsAffected = await db.Books.Where(book => book.Id == id)
                                        .ExecuteUpdateAsync(updates =>
                                            updates.SetProperty(book => book.ISBN, bookItem.ISBN)
                                                    .SetProperty(book => book.NumberOfPages, bookItem.NumberOfPages)
                                                    .SetProperty(book => book.PublishDate, bookItem.PublishDate)
                                                    .SetProperty(book => book.Title, bookItem.Title)
                                        );
            
            return rowsAffected == 0 ? TypedResults.NotFound() : TypedResults.Ok();
        });

        group.MapDelete("/{id}", async Task<Results<NotFound, Ok>> (MediaSetDbContext db, int id) => 
        {
            var rowsAffected = await db.Books.Where(book => book.Id == id).ExecuteDeleteAsync();

            return rowsAffected == 0 ? TypedResults.NotFound() : TypedResults.Ok();
        });

        return group;
    }
}