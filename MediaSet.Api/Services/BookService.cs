using System.Linq.Expressions;
using MediaSet.Api.Models;
using MongoDB.Driver;

namespace MediaSet.Api.Services;

public class BookService
{
  private readonly IMongoCollection<Book> booksCollection;

  public BookService(DatabaseService databaseService)
  {
    booksCollection = databaseService.GetCollection<Book>();
  }

  public async Task<List<Book>> GetAsync() => await booksCollection.Find(_ => true).SortBy(book => book.Title).ToListAsync();

  public async Task<List<Book>> SearchAsync(string searchText, string orderBy)
  {
    string orderByField = "";
    bool orderByAscending = true;
    if (!string.IsNullOrWhiteSpace(orderBy))
    {
      var orderByArgs = orderBy.Split(":");
      orderByField = orderByArgs[0];
      orderByAscending = string.IsNullOrWhiteSpace(orderByArgs[1]) || orderByArgs[1].ToLower().Equals("asc");
    }
    var bookSearch = booksCollection.Find(book => book.Title.ToLower().Contains(searchText.ToLower()));
    Expression<Func<Book, object>> sortFn = book => orderByField.ToLower().Equals("pages") ? book.Pages : book.Title;

    if (orderByAscending)
    {
      bookSearch.SortBy(sortFn);
    }
    else
    {
      bookSearch.SortByDescending(sortFn);
    }
    return await bookSearch.ToListAsync();
  }

  public async Task<Book?> GetAsync(string id) => await booksCollection.Find(x => x.Id == id).FirstOrDefaultAsync();

  public async Task CreateAsync(Book newBook) => await booksCollection.InsertOneAsync(newBook);

  public async Task<ReplaceOneResult> UpdateAsync(string id, Book updatedBook) => await booksCollection.ReplaceOneAsync(x => x.Id == id, updatedBook);

  public async Task<DeleteResult> RemoveAsync(string id) => await booksCollection.DeleteOneAsync(x => x.Id == id);
}