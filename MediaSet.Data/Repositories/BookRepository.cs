using MediaSet.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MediaSet.Data.Repositories;

public interface IBookRepository
{
    Task<List<Book>> GetAllBooks();
    Task<Book?> GetBookById(int id);
    Task<Book> CreateBook(Book book);
    Task<Book?> UpdateBook(Book book);
    Task<int> DeleteBookById(int id);
}

public class BookRepository : IBookRepository
{
    private readonly MediaSetDbContext db;

    public BookRepository(MediaSetDbContext mediaSetDbContext)
    {
        db = mediaSetDbContext;
    }

    public Task<List<Book>> GetAllBooks()
    {
        return db.Books.Include(book => book.Format).AsNoTracking().ToListAsync();

    }
    public Task<Book?> GetBookById(int id)
    {
        return db.Books.Include(book => book.Format).FirstOrDefaultAsync(book => book.Id == id);
    }

    public async Task<Book> CreateBook(Book book)
    {
        if (book.Format?.Id == 0)
        {
            db.Formats.Add(book.Format);
            await db.SaveChangesAsync();
            book.FormatId = book.Format.Id;
        }
        else
        {
            book.FormatId = book.Format?.Id;
            book.Format = null;
        }

        db.Books.Add(book);
        await db.SaveChangesAsync();
        if (book.FormatId != null && book.Format is null)
        {
            book.Format = await db.Formats.FirstOrDefaultAsync(format => format.Id == book.FormatId);
        }

        return book;
    }

    public async Task<Book?> UpdateBook(Book book)
    {
        if (book.Format?.Id == 0)
        {
            db.Formats.Add(book.Format);
            await db.SaveChangesAsync();
            book.FormatId = book.Format.Id;
        }
        else
        {
            book.FormatId = book.Format?.Id;
        }

        var rowsAffected = await db.Books.Where(b => b.Id == book.Id)
            .ExecuteUpdateAsync(updates =>
                updates.SetProperty(b => b.Title, book.Title)
                    .SetProperty(b => b.ISBN, book.ISBN)
                    .SetProperty(b => b.NumberOfPages, book.NumberOfPages)
                    .SetProperty(b => b.PublicationYear, book.PublicationYear)
                    .SetProperty(b => b.Plot, book.Plot)
                    .SetProperty(b => b.FormatId, book.FormatId)
            );

        if (book.FormatId != null)
        {
            book.Format = await db.Formats.FirstOrDefaultAsync(format => format.Id == book.FormatId);
        }
        
        return book;
    }

    public Task<int> DeleteBookById(int id)
    {
        return db.Books.Where(b => b.Id == id)
                    .ExecuteDeleteAsync();
    }
}