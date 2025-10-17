using MediaSet.Api.Models;

namespace MediaSet.Api.Services;

public class MetadataService : IMetadataService
{
    private readonly IEntityService<Book> booksService;
    private readonly IEntityService<Movie> movieService;
    private readonly IEntityService<Game> gameService;

    public MetadataService(IEntityService<Book> _booksService, IEntityService<Movie> _movieService, IEntityService<Game> _gameService)
    {
        booksService = _booksService;
        movieService = _movieService;
        gameService = _gameService;
    }

    public async Task<IEnumerable<string>> GetBookFormats()
    {
        var books = await booksService.GetListAsync();

        return books
          .Where(book => !string.IsNullOrWhiteSpace(book.Format))
          .Select(book => book.Format.Trim())
          .Distinct()
          .Order();
    }

    public async Task<IEnumerable<string>> GetBookAuthors()
    {
        var books = await booksService.GetListAsync();

        return books
          .Where(book => book.Authors?.Count > 0)
          .SelectMany(book => book.Authors)
          .Select(author => author.Trim())
          .Distinct()
          .Order();
    }

    public async Task<IEnumerable<string>> GetBookPublishers()
    {
        var books = await booksService.GetListAsync();

        return books
          .Where(book => !string.IsNullOrWhiteSpace(book.Publisher))
          .Select(book => book.Publisher.Trim())
          .Distinct()
          .Order();
    }

    public async Task<IEnumerable<string>> GetBookGenres()
    {
        var books = await booksService.GetListAsync();

        return books
          .Where(book => book.Genres?.Count > 0)
          .SelectMany(book => book.Genres)
          .Select(genre => genre.Trim())
          .Distinct()
          .Order();
    }

    public async Task<IEnumerable<string>> GetMovieFormats()
    {
        var movies = await movieService.GetListAsync();

        return movies
          .Where(movie => !string.IsNullOrWhiteSpace(movie.Format))
          .Select(movie => movie.Format.Trim())
          .Distinct()
          .Order();
    }

    public async Task<IEnumerable<string>> GetMovieStudios()
    {
        var movies = await movieService.GetListAsync();

        return movies
          .Where(movie => movie.Studios?.Count > 0)
          .SelectMany(movie => movie.Studios)
          .Select(studio => studio.Trim())
          .Distinct()
          .Order();
    }

    public async Task<IEnumerable<string>> GetMovieGenres()
    {
        var movies = await movieService.GetListAsync();

        return movies
          .Where(movie => movie.Genres?.Count > 0)
          .SelectMany(movie => movie.Genres)
          .Select(genre => genre.Trim())
          .Distinct()
          .Order();
    }

    public async Task<IEnumerable<string>> GetGameFormats()
    {
        var games = await gameService.GetListAsync();

        return games
          .Where(game => !string.IsNullOrWhiteSpace(game.Format))
          .Select(game => game.Format.Trim())
          .Distinct()
          .Order();
    }

    public async Task<IEnumerable<string>> GetGamePlatforms()
    {
        var games = await gameService.GetListAsync();

        return games
          .Where(game => !string.IsNullOrWhiteSpace(game.Platform))
          .Select(game => game.Platform.Trim())
          .Distinct()
          .Order();
    }

    public async Task<IEnumerable<string>> GetGameDevelopers()
    {
        var games = await gameService.GetListAsync();

        return games
          .Where(game => game.Developers?.Count > 0)
          .SelectMany(game => game.Developers)
          .Select(developer => developer.Trim())
          .Distinct()
          .Order();
    }

    public async Task<IEnumerable<string>> GetGamePublishers()
    {
        var games = await gameService.GetListAsync();

        return games
          .Where(game => game.Publishers?.Count > 0)
          .SelectMany(game => game.Publishers)
          .Select(publisher => publisher.Trim())
          .Distinct()
          .Order();
    }

    public async Task<IEnumerable<string>> GetGameGenres()
    {
        var games = await gameService.GetListAsync();

        return games
          .Where(game => game.Genres?.Count > 0)
          .SelectMany(game => game.Genres)
          .Select(genre => genre.Trim())
          .Distinct()
          .Order();
    }
}
