using NUnit.Framework;
using Moq;
using Bogus;
using MediaSet.Api.Services;
using MediaSet.Api.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace MediaSet.Api.Tests.Services;

[TestFixture]
public class MetadataServiceTests
{
    private Mock<IEntityService<Book>> _bookServiceMock;
    private Mock<IEntityService<Movie>> _movieServiceMock;
    private Mock<IEntityService<Game>> _gameServiceMock;
    private MetadataService _metadataService;
    private Faker<Book> _bookFaker;
    private Faker<Movie> _movieFaker;
    private Faker<Game> _gameFaker;

    [SetUp]
    public void Setup()
    {
        _bookServiceMock = new Mock<IEntityService<Book>>();
        _movieServiceMock = new Mock<IEntityService<Movie>>();
        _gameServiceMock = new Mock<IEntityService<Game>>();
        _metadataService = new MetadataService(
          _bookServiceMock.Object,
          _movieServiceMock.Object,
          _gameServiceMock.Object
        );

        _bookFaker = new Faker<Book>()
          .RuleFor(b => b.Id, f => f.Random.AlphaNumeric(24))
          .RuleFor(b => b.Title, f => f.Lorem.Sentence())
          .RuleFor(b => b.Format, f => f.PickRandom("Hardcover", "Paperback", "eBook", "Audiobook"))
          .RuleFor(b => b.Authors, f => new List<string> { f.Person.FullName, f.Person.FullName })
          .RuleFor(b => b.Publisher, f => f.Company.CompanyName())
          .RuleFor(b => b.Genres, f => new List<string> { f.PickRandom("Fiction", "Non-Fiction", "Mystery", "Sci-Fi") });

        _movieFaker = new Faker<Movie>()
          .RuleFor(m => m.Id, f => f.Random.AlphaNumeric(24))
          .RuleFor(m => m.Title, f => f.Lorem.Sentence())
          .RuleFor(m => m.Format, f => f.PickRandom("DVD", "Blu-ray", "4K UHD", "Digital"))
          .RuleFor(m => m.Studios, f => new List<string> { f.Company.CompanyName(), f.Company.CompanyName() })
          .RuleFor(m => m.Genres, f => new List<string> { f.PickRandom("Action", "Comedy", "Drama", "Horror") });

        _gameFaker = new Faker<Game>()
          .RuleFor(g => g.Id, f => f.Random.AlphaNumeric(24))
          .RuleFor(g => g.Title, f => f.Lorem.Sentence())
          .RuleFor(g => g.Format, f => f.PickRandom("Disc", "Cartridge", "Digital"))
          .RuleFor(g => g.Platforms, f => new List<string> { f.PickRandom("PC", "Xbox", "PlayStation", "Switch") })
          .RuleFor(g => g.Developers, f => new List<string> { f.Company.CompanyName() })
          .RuleFor(g => g.Publisher, f => f.Company.CompanyName())
          .RuleFor(g => g.Genres, f => new List<string> { f.PickRandom("Action", "RPG", "Strategy", "Sports") });
    }

    #region GetBookFormats Tests

    [Test]
    public async Task GetBookFormats_ShouldReturnDistinctFormats_WhenBooksHaveFormats()
    {
        // Arrange
        var books = new List<Book>
    {
      _bookFaker.Clone().RuleFor(b => b.Format, "Hardcover").Generate(),
      _bookFaker.Clone().RuleFor(b => b.Format, "Paperback").Generate(),
      _bookFaker.Clone().RuleFor(b => b.Format, "Hardcover").Generate(), // Duplicate
      _bookFaker.Clone().RuleFor(b => b.Format, "eBook").Generate()
    };

        _bookServiceMock.Setup(s => s.GetListAsync())
          .ReturnsAsync(books);

        // Act
        var result = await _metadataService.GetBookFormats();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count(), Is.EqualTo(3));
        Assert.That(result, Contains.Item("Hardcover"));
        Assert.That(result, Contains.Item("Paperback"));
        Assert.That(result, Contains.Item("eBook"));
        Assert.That(result, Is.Ordered); // Should be sorted
    }

    [Test]
    public async Task GetBookFormats_ShouldTrimWhitespace_FromFormats()
    {
        // Arrange
        var books = new List<Book>
    {
      _bookFaker.Clone().RuleFor(b => b.Format, "  Hardcover  ").Generate(),
      _bookFaker.Clone().RuleFor(b => b.Format, "Paperback  ").Generate(),
      _bookFaker.Clone().RuleFor(b => b.Format, "  eBook").Generate()
    };

        _bookServiceMock.Setup(s => s.GetListAsync())
          .ReturnsAsync(books);

        // Act
        var result = await _metadataService.GetBookFormats();

        // Assert
        Assert.That(result, Contains.Item("Hardcover"));
        Assert.That(result, Contains.Item("Paperback"));
        Assert.That(result, Contains.Item("eBook"));
        Assert.That(result.All(f => f == f.Trim()), Is.True);
    }

    [Test]
    public async Task GetBookFormats_ShouldExcludeEmptyFormats()
    {
        // Arrange
        var books = new List<Book>
    {
      _bookFaker.Clone().RuleFor(b => b.Format, "Hardcover").Generate(),
      _bookFaker.Clone().RuleFor(b => b.Format, "").Generate(),
      _bookFaker.Clone().RuleFor(b => b.Format, "   ").Generate(),
      _bookFaker.Clone().RuleFor(b => b.Format, "Paperback").Generate()
    };

        _bookServiceMock.Setup(s => s.GetListAsync())
          .ReturnsAsync(books);

        // Act
        var result = await _metadataService.GetBookFormats();

        // Assert
        Assert.That(result.Count(), Is.EqualTo(2));
        Assert.That(result, Contains.Item("Hardcover"));
        Assert.That(result, Contains.Item("Paperback"));
    }

    [Test]
    public async Task GetBookFormats_ShouldReturnEmpty_WhenNoBooksExist()
    {
        // Arrange
        _bookServiceMock.Setup(s => s.GetListAsync())
          .ReturnsAsync(new List<Book>());

        // Act
        var result = await _metadataService.GetBookFormats();

        // Assert
        Assert.That(result, Is.Empty);
    }

    #endregion

    #region Game Metadata Tests

    [Test]
    public async Task GetGameFormats_ShouldReturnDistinctFormats_WhenGamesHaveFormats()
    {
        var games = new List<Game>
    {
      _gameFaker.Clone().RuleFor(g => g.Format, "Disc").Generate(),
      _gameFaker.Clone().RuleFor(g => g.Format, "Digital").Generate(),
      _gameFaker.Clone().RuleFor(g => g.Format, "Disc").Generate()
    };

        _gameServiceMock.Setup(s => s.GetListAsync()).ReturnsAsync(games);

        var result = await _metadataService.GetGameFormats();

        Assert.That(result.Count(), Is.EqualTo(2));
        Assert.That(result, Contains.Item("Digital"));
        Assert.That(result, Contains.Item("Disc"));
        Assert.That(result, Is.Ordered);
    }

    [Test]
    public async Task GetGamePlatforms_ShouldReturnDistinctPlatforms()
    {
        var games = new List<Game>
    {
      _gameFaker.Clone().RuleFor(g => g.Platforms, new List<string>{ "PC", "Xbox" }).Generate(),
      _gameFaker.Clone().RuleFor(g => g.Platforms, new List<string>{ "Xbox", "Switch" }).Generate(),
    };

        _gameServiceMock.Setup(s => s.GetListAsync()).ReturnsAsync(games);

        var result = await _metadataService.GetGamePlatforms();

        Assert.That(result.Count(), Is.EqualTo(3));
        Assert.That(result, Does.Contain("PC"));
        Assert.That(result, Does.Contain("Xbox"));
        Assert.That(result, Does.Contain("Switch"));
        Assert.That(result, Is.Ordered);
    }

    [Test]
    public async Task GetGameDevelopers_ShouldReturnDistinctDevelopers()
    {
        var games = new List<Game>
    {
      _gameFaker.Clone().RuleFor(g => g.Developers, new List<string>{ "Studio A", "Studio B" }).Generate(),
      _gameFaker.Clone().RuleFor(g => g.Developers, new List<string>{ "Studio B", "Studio C" }).Generate(),
    };

        _gameServiceMock.Setup(s => s.GetListAsync()).ReturnsAsync(games);

        var result = await _metadataService.GetGameDevelopers();

        Assert.That(result.Count(), Is.EqualTo(3));
        Assert.That(result, Does.Contain("Studio A"));
        Assert.That(result, Does.Contain("Studio B"));
        Assert.That(result, Does.Contain("Studio C"));
        Assert.That(result, Is.Ordered);
    }

    [Test]
    public async Task GetGamePublishers_ShouldReturnDistinctPublishers()
    {
        var games = new List<Game>
    {
      _gameFaker.Clone().RuleFor(g => g.Publisher, "Nintendo").Generate(),
      _gameFaker.Clone().RuleFor(g => g.Publisher, "Sony").Generate(),
      _gameFaker.Clone().RuleFor(g => g.Publisher, "Nintendo").Generate(),
    };

        _gameServiceMock.Setup(s => s.GetListAsync()).ReturnsAsync(games);

        var result = await _metadataService.GetGamePublishers();

        Assert.That(result.Count(), Is.EqualTo(2));
        Assert.That(result, Does.Contain("Nintendo"));
        Assert.That(result, Does.Contain("Sony"));
        Assert.That(result, Is.Ordered);
    }

    [Test]
    public async Task GetGameGenres_ShouldReturnDistinctGenres()
    {
        var games = new List<Game>
    {
      _gameFaker.Clone().RuleFor(g => g.Genres, new List<string>{ "Action", "RPG" }).Generate(),
      _gameFaker.Clone().RuleFor(g => g.Genres, new List<string>{ "RPG", "Strategy" }).Generate(),
    };

        _gameServiceMock.Setup(s => s.GetListAsync()).ReturnsAsync(games);

        var result = await _metadataService.GetGameGenres();

        Assert.That(result.Count(), Is.EqualTo(3));
        Assert.That(result, Does.Contain("Action"));
        Assert.That(result, Does.Contain("RPG"));
        Assert.That(result, Does.Contain("Strategy"));
        Assert.That(result, Is.Ordered);
    }

    [Test]
    public async Task GetGameFormats_ShouldCallGameServiceGetListAsync_Once()
    {
        _gameServiceMock.Setup(s => s.GetListAsync()).ReturnsAsync(new List<Game>());

        await _metadataService.GetGameFormats();

        _gameServiceMock.Verify(s => s.GetListAsync(), Times.Once);
    }

    #endregion

    #region GetBookAuthors Tests

    [Test]
    public async Task GetBookAuthors_ShouldReturnDistinctAuthors_WhenBooksHaveAuthors()
    {
        // Arrange
        var books = new List<Book>
    {
      _bookFaker.Clone().RuleFor(b => b.Authors, new List<string> { "J.K. Rowling", "Stephen King" }).Generate(),
      _bookFaker.Clone().RuleFor(b => b.Authors, new List<string> { "Stephen King", "George R.R. Martin" }).Generate(),
      _bookFaker.Clone().RuleFor(b => b.Authors, new List<string> { "Brandon Sanderson" }).Generate()
    };

        _bookServiceMock.Setup(s => s.GetListAsync())
          .ReturnsAsync(books);

        // Act
        var result = await _metadataService.GetBookAuthors();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count(), Is.EqualTo(4));
        Assert.That(result, Contains.Item("J.K. Rowling"));
        Assert.That(result, Contains.Item("Stephen King"));
        Assert.That(result, Contains.Item("George R.R. Martin"));
        Assert.That(result, Contains.Item("Brandon Sanderson"));
        Assert.That(result, Is.Ordered);
    }

    [Test]
    public async Task GetBookAuthors_ShouldTrimWhitespace_FromAuthors()
    {
        // Arrange
        var books = new List<Book>
    {
      _bookFaker.Clone().RuleFor(b => b.Authors, new List<string> { "  Stephen King  ", "J.K. Rowling  " }).Generate()
    };

        _bookServiceMock.Setup(s => s.GetListAsync())
          .ReturnsAsync(books);

        // Act
        var result = await _metadataService.GetBookAuthors();

        // Assert
        Assert.That(result, Contains.Item("Stephen King"));
        Assert.That(result, Contains.Item("J.K. Rowling"));
        Assert.That(result.All(a => a == a.Trim()), Is.True);
    }

    [Test]
    public async Task GetBookAuthors_ShouldExcludeBooksWithNoAuthors()
    {
        // Arrange
        var books = new List<Book>
    {
      _bookFaker.Clone().RuleFor(b => b.Authors, new List<string> { "Stephen King" }).Generate(),
      _bookFaker.Clone().RuleFor(b => b.Authors, new List<string>()).Generate(),
      _bookFaker.Clone().RuleFor(b => b.Authors, (List<string>)null).Generate()
    };

        _bookServiceMock.Setup(s => s.GetListAsync())
          .ReturnsAsync(books);

        // Act
        var result = await _metadataService.GetBookAuthors();

        // Assert
        Assert.That(result.Count(), Is.EqualTo(1));
        Assert.That(result, Contains.Item("Stephen King"));
    }

    [Test]
    public async Task GetBookAuthors_ShouldReturnEmpty_WhenNoBooksExist()
    {
        // Arrange
        _bookServiceMock.Setup(s => s.GetListAsync())
          .ReturnsAsync(new List<Book>());

        // Act
        var result = await _metadataService.GetBookAuthors();

        // Assert
        Assert.That(result, Is.Empty);
    }

    #endregion

    #region GetBookPublishers Tests

    [Test]
    public async Task GetBookPublishers_ShouldReturnDistinctPublishers_WhenBooksHavePublishers()
    {
        // Arrange
        var books = new List<Book>
    {
      _bookFaker.Clone().RuleFor(b => b.Publisher, "Penguin Random House").Generate(),
      _bookFaker.Clone().RuleFor(b => b.Publisher, "HarperCollins").Generate(),
      _bookFaker.Clone().RuleFor(b => b.Publisher, "Penguin Random House").Generate(), // Duplicate
      _bookFaker.Clone().RuleFor(b => b.Publisher, "Simon & Schuster").Generate()
    };

        _bookServiceMock.Setup(s => s.GetListAsync())
          .ReturnsAsync(books);

        // Act
        var result = await _metadataService.GetBookPublishers();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count(), Is.EqualTo(3));
        Assert.That(result, Contains.Item("Penguin Random House"));
        Assert.That(result, Contains.Item("HarperCollins"));
        Assert.That(result, Contains.Item("Simon & Schuster"));
        Assert.That(result, Is.Ordered);
    }

    [Test]
    public async Task GetBookPublishers_ShouldTrimWhitespace_FromPublishers()
    {
        // Arrange
        var books = new List<Book>
    {
      _bookFaker.Clone().RuleFor(b => b.Publisher, "  Penguin Random House  ").Generate(),
      _bookFaker.Clone().RuleFor(b => b.Publisher, "HarperCollins  ").Generate()
    };

        _bookServiceMock.Setup(s => s.GetListAsync())
          .ReturnsAsync(books);

        // Act
        var result = await _metadataService.GetBookPublishers();

        // Assert
        Assert.That(result, Contains.Item("Penguin Random House"));
        Assert.That(result, Contains.Item("HarperCollins"));
        Assert.That(result.All(p => p == p.Trim()), Is.True);
    }

    [Test]
    public async Task GetBookPublishers_ShouldExcludeEmptyPublishers()
    {
        // Arrange
        var books = new List<Book>
    {
      _bookFaker.Clone().RuleFor(b => b.Publisher, "Penguin Random House").Generate(),
      _bookFaker.Clone().RuleFor(b => b.Publisher, "").Generate(),
      _bookFaker.Clone().RuleFor(b => b.Publisher, "   ").Generate(),
      _bookFaker.Clone().RuleFor(b => b.Publisher, "HarperCollins").Generate()
    };

        _bookServiceMock.Setup(s => s.GetListAsync())
          .ReturnsAsync(books);

        // Act
        var result = await _metadataService.GetBookPublishers();

        // Assert
        Assert.That(result.Count(), Is.EqualTo(2));
        Assert.That(result, Contains.Item("Penguin Random House"));
        Assert.That(result, Contains.Item("HarperCollins"));
    }

    [Test]
    public async Task GetBookPublishers_ShouldReturnEmpty_WhenNoBooksExist()
    {
        // Arrange
        _bookServiceMock.Setup(s => s.GetListAsync())
          .ReturnsAsync(new List<Book>());

        // Act
        var result = await _metadataService.GetBookPublishers();

        // Assert
        Assert.That(result, Is.Empty);
    }

    #endregion

    #region GetBookGenres Tests

    [Test]
    public async Task GetBookGenres_ShouldReturnDistinctGenres_WhenBooksHaveGenres()
    {
        // Arrange
        var books = new List<Book>
    {
      _bookFaker.Clone().RuleFor(b => b.Genres, new List<string> { "Fiction", "Mystery" }).Generate(),
      _bookFaker.Clone().RuleFor(b => b.Genres, new List<string> { "Mystery", "Thriller" }).Generate(),
      _bookFaker.Clone().RuleFor(b => b.Genres, new List<string> { "Sci-Fi" }).Generate()
    };

        _bookServiceMock.Setup(s => s.GetListAsync())
          .ReturnsAsync(books);

        // Act
        var result = await _metadataService.GetBookGenres();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count(), Is.EqualTo(4));
        Assert.That(result, Contains.Item("Fiction"));
        Assert.That(result, Contains.Item("Mystery"));
        Assert.That(result, Contains.Item("Thriller"));
        Assert.That(result, Contains.Item("Sci-Fi"));
        Assert.That(result, Is.Ordered);
    }

    [Test]
    public async Task GetBookGenres_ShouldTrimWhitespace_FromGenres()
    {
        // Arrange
        var books = new List<Book>
    {
      _bookFaker.Clone().RuleFor(b => b.Genres, new List<string> { "  Fiction  ", "Mystery  " }).Generate()
    };

        _bookServiceMock.Setup(s => s.GetListAsync())
          .ReturnsAsync(books);

        // Act
        var result = await _metadataService.GetBookGenres();

        // Assert
        Assert.That(result, Contains.Item("Fiction"));
        Assert.That(result, Contains.Item("Mystery"));
        Assert.That(result.All(g => g == g.Trim()), Is.True);
    }

    [Test]
    public async Task GetBookGenres_ShouldExcludeBooksWithNoGenres()
    {
        // Arrange
        var books = new List<Book>
    {
      _bookFaker.Clone().RuleFor(b => b.Genres, new List<string> { "Fiction" }).Generate(),
      _bookFaker.Clone().RuleFor(b => b.Genres, new List<string>()).Generate(),
      _bookFaker.Clone().RuleFor(b => b.Genres, (List<string>)null).Generate()
    };

        _bookServiceMock.Setup(s => s.GetListAsync())
          .ReturnsAsync(books);

        // Act
        var result = await _metadataService.GetBookGenres();

        // Assert
        Assert.That(result.Count(), Is.EqualTo(1));
        Assert.That(result, Contains.Item("Fiction"));
    }

    [Test]
    public async Task GetBookGenres_ShouldReturnEmpty_WhenNoBooksExist()
    {
        // Arrange
        _bookServiceMock.Setup(s => s.GetListAsync())
          .ReturnsAsync(new List<Book>());

        // Act
        var result = await _metadataService.GetBookGenres();

        // Assert
        Assert.That(result, Is.Empty);
    }

    #endregion

    #region GetMovieFormats Tests

    [Test]
    public async Task GetMovieFormats_ShouldReturnDistinctFormats_WhenMoviesHaveFormats()
    {
        // Arrange
        var movies = new List<Movie>
    {
      _movieFaker.Clone().RuleFor(m => m.Format, "DVD").Generate(),
      _movieFaker.Clone().RuleFor(m => m.Format, "Blu-ray").Generate(),
      _movieFaker.Clone().RuleFor(m => m.Format, "DVD").Generate(), // Duplicate
      _movieFaker.Clone().RuleFor(m => m.Format, "4K UHD").Generate()
    };

        _movieServiceMock.Setup(s => s.GetListAsync())
          .ReturnsAsync(movies);

        // Act
        var result = await _metadataService.GetMovieFormats();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count(), Is.EqualTo(3));
        Assert.That(result, Contains.Item("DVD"));
        Assert.That(result, Contains.Item("Blu-ray"));
        Assert.That(result, Contains.Item("4K UHD"));
        Assert.That(result, Is.Ordered);
    }

    [Test]
    public async Task GetMovieFormats_ShouldTrimWhitespace_FromFormats()
    {
        // Arrange
        var movies = new List<Movie>
    {
      _movieFaker.Clone().RuleFor(m => m.Format, "  DVD  ").Generate(),
      _movieFaker.Clone().RuleFor(m => m.Format, "Blu-ray  ").Generate()
    };

        _movieServiceMock.Setup(s => s.GetListAsync())
          .ReturnsAsync(movies);

        // Act
        var result = await _metadataService.GetMovieFormats();

        // Assert
        Assert.That(result, Contains.Item("DVD"));
        Assert.That(result, Contains.Item("Blu-ray"));
        Assert.That(result.All(f => f == f.Trim()), Is.True);
    }

    [Test]
    public async Task GetMovieFormats_ShouldExcludeEmptyFormats()
    {
        // Arrange
        var movies = new List<Movie>
    {
      _movieFaker.Clone().RuleFor(m => m.Format, "DVD").Generate(),
      _movieFaker.Clone().RuleFor(m => m.Format, "").Generate(),
      _movieFaker.Clone().RuleFor(m => m.Format, "   ").Generate(),
      _movieFaker.Clone().RuleFor(m => m.Format, "Blu-ray").Generate()
    };

        _movieServiceMock.Setup(s => s.GetListAsync())
          .ReturnsAsync(movies);

        // Act
        var result = await _metadataService.GetMovieFormats();

        // Assert
        Assert.That(result.Count(), Is.EqualTo(2));
        Assert.That(result, Contains.Item("DVD"));
        Assert.That(result, Contains.Item("Blu-ray"));
    }

    [Test]
    public async Task GetMovieFormats_ShouldReturnEmpty_WhenNoMoviesExist()
    {
        // Arrange
        _movieServiceMock.Setup(s => s.GetListAsync())
          .ReturnsAsync(new List<Movie>());

        // Act
        var result = await _metadataService.GetMovieFormats();

        // Assert
        Assert.That(result, Is.Empty);
    }

    #endregion

    #region GetMovieStudios Tests

    [Test]
    public async Task GetMovieStudios_ShouldReturnDistinctStudios_WhenMoviesHaveStudios()
    {
        // Arrange
        var movies = new List<Movie>
    {
      _movieFaker.Clone().RuleFor(m => m.Studios, new List<string> { "Warner Bros", "Universal" }).Generate(),
      _movieFaker.Clone().RuleFor(m => m.Studios, new List<string> { "Universal", "Paramount" }).Generate(),
      _movieFaker.Clone().RuleFor(m => m.Studios, new List<string> { "Disney" }).Generate()
    };

        _movieServiceMock.Setup(s => s.GetListAsync())
          .ReturnsAsync(movies);

        // Act
        var result = await _metadataService.GetMovieStudios();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count(), Is.EqualTo(4));
        Assert.That(result, Contains.Item("Warner Bros"));
        Assert.That(result, Contains.Item("Universal"));
        Assert.That(result, Contains.Item("Paramount"));
        Assert.That(result, Contains.Item("Disney"));
        Assert.That(result, Is.Ordered);
    }

    [Test]
    public async Task GetMovieStudios_ShouldTrimWhitespace_FromStudios()
    {
        // Arrange
        var movies = new List<Movie>
    {
      _movieFaker.Clone().RuleFor(m => m.Studios, new List<string> { "  Warner Bros  ", "Universal  " }).Generate()
    };

        _movieServiceMock.Setup(s => s.GetListAsync())
          .ReturnsAsync(movies);

        // Act
        var result = await _metadataService.GetMovieStudios();

        // Assert
        Assert.That(result, Contains.Item("Warner Bros"));
        Assert.That(result, Contains.Item("Universal"));
        Assert.That(result.All(s => s == s.Trim()), Is.True);
    }

    [Test]
    public async Task GetMovieStudios_ShouldExcludeMoviesWithNoStudios()
    {
        // Arrange
        var movies = new List<Movie>
    {
      _movieFaker.Clone().RuleFor(m => m.Studios, new List<string> { "Universal" }).Generate(),
      _movieFaker.Clone().RuleFor(m => m.Studios, new List<string>()).Generate(),
      _movieFaker.Clone().RuleFor(m => m.Studios, (List<string>)null).Generate()
    };

        _movieServiceMock.Setup(s => s.GetListAsync())
          .ReturnsAsync(movies);

        // Act
        var result = await _metadataService.GetMovieStudios();

        // Assert
        Assert.That(result.Count(), Is.EqualTo(1));
        Assert.That(result, Contains.Item("Universal"));
    }

    [Test]
    public async Task GetMovieStudios_ShouldReturnEmpty_WhenNoMoviesExist()
    {
        // Arrange
        _movieServiceMock.Setup(s => s.GetListAsync())
          .ReturnsAsync(new List<Movie>());

        // Act
        var result = await _metadataService.GetMovieStudios();

        // Assert
        Assert.That(result, Is.Empty);
    }

    #endregion

    #region GetMovieGenres Tests

    [Test]
    public async Task GetMovieGenres_ShouldReturnDistinctGenres_WhenMoviesHaveGenres()
    {
        // Arrange
        var movies = new List<Movie>
    {
      _movieFaker.Clone().RuleFor(m => m.Genres, new List<string> { "Action", "Adventure" }).Generate(),
      _movieFaker.Clone().RuleFor(m => m.Genres, new List<string> { "Adventure", "Comedy" }).Generate(),
      _movieFaker.Clone().RuleFor(m => m.Genres, new List<string> { "Drama" }).Generate()
    };

        _movieServiceMock.Setup(s => s.GetListAsync())
          .ReturnsAsync(movies);

        // Act
        var result = await _metadataService.GetMovieGenres();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count(), Is.EqualTo(4));
        Assert.That(result, Contains.Item("Action"));
        Assert.That(result, Contains.Item("Adventure"));
        Assert.That(result, Contains.Item("Comedy"));
        Assert.That(result, Contains.Item("Drama"));
        Assert.That(result, Is.Ordered);
    }

    [Test]
    public async Task GetMovieGenres_ShouldTrimWhitespace_FromGenres()
    {
        // Arrange
        var movies = new List<Movie>
    {
      _movieFaker.Clone().RuleFor(m => m.Genres, new List<string> { "  Action  ", "Comedy  " }).Generate()
    };

        _movieServiceMock.Setup(s => s.GetListAsync())
          .ReturnsAsync(movies);

        // Act
        var result = await _metadataService.GetMovieGenres();

        // Assert
        Assert.That(result, Contains.Item("Action"));
        Assert.That(result, Contains.Item("Comedy"));
        Assert.That(result.All(g => g == g.Trim()), Is.True);
    }

    [Test]
    public async Task GetMovieGenres_ShouldExcludeMoviesWithNoGenres()
    {
        // Arrange
        var movies = new List<Movie>
    {
      _movieFaker.Clone().RuleFor(m => m.Genres, new List<string> { "Action" }).Generate(),
      _movieFaker.Clone().RuleFor(m => m.Genres, new List<string>()).Generate(),
      _movieFaker.Clone().RuleFor(m => m.Genres, (List<string>)null).Generate()
    };

        _movieServiceMock.Setup(s => s.GetListAsync())
          .ReturnsAsync(movies);

        // Act
        var result = await _metadataService.GetMovieGenres();

        // Assert
        Assert.That(result.Count(), Is.EqualTo(1));
        Assert.That(result, Contains.Item("Action"));
    }

    [Test]
    public async Task GetMovieGenres_ShouldReturnEmpty_WhenNoMoviesExist()
    {
        // Arrange
        _movieServiceMock.Setup(s => s.GetListAsync())
          .ReturnsAsync(new List<Movie>());

        // Act
        var result = await _metadataService.GetMovieGenres();

        // Assert
        Assert.That(result, Is.Empty);
    }

    #endregion

    #region Service Mock Verification Tests

    [Test]
    public async Task GetBookFormats_ShouldCallBookServiceGetListAsync_Once()
    {
        // Arrange
        _bookServiceMock.Setup(s => s.GetListAsync())
          .ReturnsAsync(new List<Book>());

        // Act
        await _metadataService.GetBookFormats();

        // Assert
        _bookServiceMock.Verify(s => s.GetListAsync(), Times.Once);
    }

    [Test]
    public async Task GetMovieFormats_ShouldCallMovieServiceGetListAsync_Once()
    {
        // Arrange
        _movieServiceMock.Setup(s => s.GetListAsync())
          .ReturnsAsync(new List<Movie>());

        // Act
        await _metadataService.GetMovieFormats();

        // Assert
        _movieServiceMock.Verify(s => s.GetListAsync(), Times.Once);
    }

    #endregion
}
