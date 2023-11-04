using MediaSet.Api.Filters;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace MediaSet.Api.BookApi;

internal static class BookApi
{
    public static RouteGroupBuilder MapBooks(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/books");

        group.WithPrameterValidation(typeof(Book));

        group.MapGet("/", async (MediaSetDbContext db) =>
        {
            return await db.Books.Include(book => book.Format).AsNoTracking().ToListAsync();
        });

        group.MapGet("/{id}", async Task<Results<Ok<Book>, NotFound>> (MediaSetDbContext db, int id) =>
        {
            return await db.Books.Include(book => book.Format).FirstOrDefaultAsync(book => book.Id == id) switch 
            {
                Book book => TypedResults.Ok(book),
                _ => TypedResults.NotFound()
            };
        });

        group.MapPost("/", async Task<Created<Book>> (MediaSetDbContext db, CreateBook createBook) =>
        {
            var book = createBook.AsBook();
            db.Books.Add(book);
            await db.SaveChangesAsync();
            if (book.FormatId != null)
            {
                book.Format = await db.Formats.FirstOrDefaultAsync(format => format.Id == book.FormatId);
            }

            return TypedResults.Created($"/books/{book.Id}", book);
        });

        group.MapPut("/{id}", async Task<Results<Ok<Book>, NotFound, BadRequest<string>>> (MediaSetDbContext db, int id, UpdateBook book) =>
        {
            if (id != book.Id)
            {
                return TypedResults.BadRequest("Book Id and route id do not match");
            }

            if (book.Format?.Id == 0)
            {
                db.Formats.Add(book.Format);
                await db.SaveChangesAsync();
            }

            var rowsAffected = await db.Books.Where(b => b.Id == id)
                .ExecuteUpdateAsync(updates =>
                    updates.SetProperty(b => b.Title, book.Title)
                        .SetProperty(b => b.ISBN, book.ISBN)
                        .SetProperty(b => b.NumberOfPages, book.NumberOfPages)
                        .SetProperty(b => b.PublicationYear, book.PublicationYear)
                        .SetProperty(b => b.Plot, book.Plot)
                        .SetProperty(b => b.FormatId, book.Format == null ? null : book.Format.Id)
                );
            
            return rowsAffected == 0 ? TypedResults.NotFound() : TypedResults.Ok(book.AsBook());
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