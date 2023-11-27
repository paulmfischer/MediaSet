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
        return db.Books
            .Include(book => book.Format)
            .Include(book => book.Genre)
            .AsNoTracking().ToListAsync();

    }
    public Task<Book?> GetBookById(int id)
    {
        return db.Books
            .Include(book => book.Format)
            .Include(book => book.Genre)
            .FirstOrDefaultAsync(book => book.Id == id);
    }

    public async Task<Book> CreateBook(Book book)
    {
        db.Books.Add(book);
        await db.SaveChangesAsync();

        return book;
    }

    public async Task<Book?> UpdateBook(Book book)
    {
        Book? dbBook = await GetBookById(book.Id);
        if (dbBook is null)
        {
            return null;
        }

        dbBook.Format = book.Format;
        dbBook.Genre = book.Genre;
        dbBook.ISBN = book.ISBN;
        dbBook.NumberOfPages = book.NumberOfPages;
        dbBook.Plot = book.Plot;
        dbBook.PublicationYear = book.PublicationYear;
        dbBook.Title = book.Title;

        await db.SaveChangesAsync();
        return dbBook;
    }

    public Task<int> DeleteBookById(int id)
    {
        return db.Books.Where(b => b.Id == id)
                    .ExecuteDeleteAsync();
    }
}