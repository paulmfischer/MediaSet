using System.Linq.Expressions;
using MediaSet.Api.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace MediaSet.Api.Services;

public class BookService
{
  private readonly IMongoCollection<Book> _booksCollection;

  public BookService(IOptions<MediaSetDatabaseSettings> mediaSetDatabaseSettings)
  {
    var dbSettings = mediaSetDatabaseSettings.Value;
    var mongoClient = new MongoClient(dbSettings.ConnectionString);
    var mongoDatabase = mongoClient.GetDatabase(dbSettings.DatabaseName);
    Console.WriteLine("Initializing db - Url: {0} ; Name: {1} ; Collection: {2}", dbSettings.ConnectionString, dbSettings.DatabaseName, dbSettings.BooksCollectionName);

    _booksCollection = mongoDatabase.GetCollection<Book>(dbSettings.BooksCollectionName);
  }

  public async Task<List<Book>> GetAsync() => await _booksCollection.Find(_ => true).SortBy(book => book.Title).ToListAsync();
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
    var bookSearch = _booksCollection.Find(book => book.Title.ToLower().Contains(searchText.ToLower()));
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

  public async Task<Book?> GetAsync(string id) => await _booksCollection.Find(x => x.Id == id).FirstOrDefaultAsync();

  public async Task CreateAsync(Book newBook) => await _booksCollection.InsertOneAsync(newBook);

  public async Task<ReplaceOneResult> UpdateAsync(string id, Book updatedBook) => await _booksCollection.ReplaceOneAsync(x => x.Id == id, updatedBook);

  public async Task<DeleteResult> RemoveAsync(string id) => await _booksCollection.DeleteOneAsync(x => x.Id == id);
}