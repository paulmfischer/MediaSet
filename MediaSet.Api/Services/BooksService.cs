using MediaSet.Api.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace MediaSet.Api.Services;

public class BooksService
{
  private readonly IMongoCollection<Book> _booksCollection;

  public BooksService(IOptions<MediaSetDatabaseSettings> mediaSetDatabaseSettings)
  {
    var dbSettings = mediaSetDatabaseSettings.Value;
    var mongoClient = new MongoClient(dbSettings.ConnectionString);
    var mongoDatabase = mongoClient.GetDatabase(dbSettings.DatabaseName);

    _booksCollection = mongoDatabase.GetCollection<Book>(dbSettings.BooksCollectionName);
  }

  public async Task<List<Book>> GetAsync() => await _booksCollection.Find(_ => true).SortBy(book => book.Title).ToListAsync();
  public List<Book> SearchAsync(string searchText) => _booksCollection.Find(book => book.Title.ToLower().Contains(searchText.ToLower())).SortBy(book => book.Title).ToList();

  public async Task<Book?> GetAsync(string id) => await _booksCollection.Find(x => x.Id == id).FirstOrDefaultAsync();

  public async Task CreateAsync(Book newBook) => await _booksCollection.InsertOneAsync(newBook);

  public async Task<ReplaceOneResult> UpdateAsync(string id, Book updatedBook) => await _booksCollection.ReplaceOneAsync(x => x.Id == id, updatedBook);

  public async Task<DeleteResult> RemoveAsync(string id) => await _booksCollection.DeleteOneAsync(x => x.Id == id);
}