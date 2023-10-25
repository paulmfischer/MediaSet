using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace MediaSet.BookApi;

internal static class BookApi
{
    public static RouteGroupBuilder MapBooks(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/books");

        // rate limit
        // group.RequirePerUserRateLimit();

        // validate parameters
        // group.WithParameterValidation(typeof(BookItem));

        group.MapGet("/", async (MediaSetDbContext db) =>
        {
            return await db.Books.AsNoTracking().ToListAsync();
        });

        group.MapGet("/{id}", async Task<Results<Ok<Book>, NotFound>> (MediaSetDbContext db, int id) =>
        {
            return await db.Books.FindAsync(id) switch 
            {
                Book book => TypedResults.Ok(book),
                _ => TypedResults.NotFound()
            };
        });

        group.MapPost("/", async Task<Created<Book>> (MediaSetDbContext db, Book book) =>
        {
            db.Books.Add(book);
            await db.SaveChangesAsync();

            return TypedResults.Created($"/books/{book.Id}", book);
        });

        group.MapPut("/{id}", async Task<Results<Ok, NotFound, BadRequest<string>>> (MediaSetDbContext db, int id, Book book) =>
        {
            if (id != book.Id)
            {
                return TypedResults.BadRequest("Book Id and route id do not match");
            }

            var rowsAffected = await db.Books.Where(b => b.Id == id)
                .ExecuteUpdateAsync(updates =>
                    updates.SetProperty(b => b.Name, book.Name)
                );
            
            return rowsAffected == 0 ? TypedResults.NotFound() : TypedResults.Ok();
        });

        group.MapDelete("/{id}", async Task<Results<NotFound, Ok>> (MediaSetDbContext db, int id) =>
        {
            var rowsAffected = await db.Books.Where(b => b.Id == id)
                                    .ExecuteDeleteAsync();
            
            return rowsAffected == 0 ? TypedResults.NotFound() : TypedResults.Ok();
        });

        return group;
    }
}