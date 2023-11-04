using Microsoft.EntityFrameworkCore;

namespace MediaSet.Api.BookApi;

internal static class FormatApi
{
    public static RouteGroupBuilder MapFormat(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/formats");

        // group.WithPrameterValidation(typeof(Book));

        group.MapGet("/", async (MediaSetDbContext db) =>
        {
            return await db.Formats.AsNoTracking().ToListAsync();
        });

        // group.MapGet("/{id}", async Task<Results<Ok<Book>, NotFound>> (MediaSetDbContext db, int id) =>
        // {
        //     return await db.Books.Include(book => book.Format).FirstOrDefaultAsync(book => book.Id == id) switch
        //     {
        //         Book book => TypedResults.Ok(book),
        //         _ => TypedResults.NotFound()
        //     };
        // });

        // group.MapPost("/", async Task<Created<Book>> (MediaSetDbContext db, CreateBook createBook) =>
        // {
        //     var book = createBook.AsBook();
        //     db.Books.Add(book);
        //     await db.SaveChangesAsync();

        //     return TypedResults.Created($"/books/{book.Id}", book);
        // });

        // group.MapPut("/{id}", async Task<Results<Ok<Book>, NotFound, BadRequest<string>>> (MediaSetDbContext db, int id, Book book) =>
        // {
        //     if (id != book.Id)
        //     {
        //         return TypedResults.BadRequest("Book Id and route id do not match");
        //     }

        //     if (book.Format?.Id == 0)
        //     {
        //         var existingFormat = await db.Formats.FirstOrDefaultAsync(x => x.Name.ToLower() == book.Format.Name.ToLower());
        //         if (existingFormat is null)
        //         {
        //             db.Formats.Add(book.Format);
        //             await db.SaveChangesAsync();
        //             book.FormatId = book.Format.Id;
        //         }
        //         else
        //         {
        //             book.FormatId = existingFormat.Id;
        //         }
        //     }

        //     var rowsAffected = await db.Books.Where(b => b.Id == id)
        //         .ExecuteUpdateAsync(updates =>
        //             updates.SetProperty(b => b.Title, book.Title)
        //                 .SetProperty(b => b.ISBN, book.ISBN)
        //                 .SetProperty(b => b.NumberOfPages, book.NumberOfPages)
        //                 .SetProperty(b => b.PublicationYear, book.PublicationYear)
        //                 .SetProperty(b => b.Plot, book.Plot)
        //                 .SetProperty(b => b.FormatId, book.FormatId)
        //         );

        //     return rowsAffected == 0 ? TypedResults.NotFound() : TypedResults.Ok(book);
        // });

        // group.MapDelete("/{id}", async Task<Results<NotFound, Ok>> (MediaSetDbContext db, int id) =>
        // {
        //     var rowsAffected = await db.Books.Where(b => b.Id == id)
        //                             .ExecuteDeleteAsync();

        //     return rowsAffected == 0 ? TypedResults.NotFound() : TypedResults.Ok();
        // });

        return group;
    }
}