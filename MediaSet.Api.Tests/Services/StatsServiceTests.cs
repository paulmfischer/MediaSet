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
public class StatsServiceTests
{
  private Mock<IEntityService<Book>> _bookServiceMock;
  private Mock<IEntityService<Movie>> _movieServiceMock;
  private Mock<IEntityService<Game>> _gameServiceMock;
  private StatsService _statsService;
  private Faker<Book> _bookFaker;
  private Faker<Movie> _movieFaker;
  private Faker<Game> _gameFaker;

  [SetUp]
  public void Setup()
  {
    _bookServiceMock = new Mock<IEntityService<Book>>();
    _movieServiceMock = new Mock<IEntityService<Movie>>();
    _gameServiceMock = new Mock<IEntityService<Game>>();
    _statsService = new StatsService(_bookServiceMock.Object, _movieServiceMock.Object, _gameServiceMock.Object);

    _bookFaker = new Faker<Book>()
      .RuleFor(b => b.Id, f => f.Random.AlphaNumeric(24))
      .RuleFor(b => b.Title, f => f.Lorem.Sentence())
      .RuleFor(b => b.Format, f => f.PickRandom("Hardcover", "Paperback", "eBook", "Audiobook"))
      .RuleFor(b => b.Pages, f => f.Random.Int(100, 1000))
      .RuleFor(b => b.Authors, f => new List<string> { f.Person.FullName, f.Person.FullName })
      .RuleFor(b => b.Genres, f => new List<string> { f.PickRandom("Fiction", "Non-Fiction", "Mystery", "Sci-Fi") });

    _movieFaker = new Faker<Movie>()
      .RuleFor(m => m.Id, f => f.Random.AlphaNumeric(24))
      .RuleFor(m => m.Title, f => f.Lorem.Sentence())
      .RuleFor(m => m.Format, f => f.PickRandom("DVD", "Blu-ray", "4K UHD", "Digital"))
      .RuleFor(m => m.IsTvSeries, f => f.Random.Bool())
      .RuleFor(m => m.Studios, f => new List<string> { f.Company.CompanyName() })
      .RuleFor(m => m.Genres, f => new List<string> { f.PickRandom("Action", "Comedy", "Drama", "Horror") });

    _gameFaker = new Faker<Game>()
      .RuleFor(g => g.Id, f => f.Random.AlphaNumeric(24))
      .RuleFor(g => g.Title, f => f.Lorem.Sentence())
      .RuleFor(g => g.Format, f => f.PickRandom("Disc", "Cartridge", "Digital"))
      .RuleFor(g => g.Platforms, f => new List<string> { f.PickRandom("PC", "PlayStation", "Xbox", "Switch") })
      .RuleFor(g => g.Developers, f => new List<string> { f.Company.CompanyName() })
      .RuleFor(g => g.Genres, f => new List<string> { f.PickRandom("Action", "RPG", "Strategy", "Sports") });
  }

  #region GetMediaStatsAsync Tests

  [Test]
  public async Task GetMediaStatsAsync_ShouldReturnStats_WithCorrectBookAndMovieCounts()
  {
    // Arrange
    var books = _bookFaker.Generate(5);
    var movies = _movieFaker.Generate(3);

    _bookServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(books);
    _movieServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(movies);
    _gameServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Game>());

    // Act
    var result = await _statsService.GetMediaStatsAsync();

    // Assert
    Assert.That(result, Is.Not.Null);
    Assert.That(result.BookStats.Total, Is.EqualTo(5));
    Assert.That(result.MovieStats.Total, Is.EqualTo(3));
  }

  [Test]
  public async Task GetMediaStatsAsync_ShouldReturnZeroCounts_WhenNoMediaExists()
  {
    // Arrange
    _bookServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Book>());
    _movieServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Movie>());
    _gameServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Game>());

    // Act
    var result = await _statsService.GetMediaStatsAsync();

    // Assert
    Assert.That(result.BookStats.Total, Is.EqualTo(0));
    Assert.That(result.MovieStats.Total, Is.EqualTo(0));
    Assert.That(result.BookStats.TotalFormats, Is.EqualTo(0));
    Assert.That(result.MovieStats.TotalFormats, Is.EqualTo(0));
    Assert.That(result.BookStats.TotalPages, Is.EqualTo(0));
    Assert.That(result.BookStats.UniqueAuthors, Is.EqualTo(0));
    Assert.That(result.MovieStats.TotalTvSeries, Is.EqualTo(0));
  }

  [Test]
  public async Task GetMediaStatsAsync_ShouldCalculateBookFormatsCorrectly()
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
    _movieServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Movie>());
    _gameServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Game>());

    // Act
    var result = await _statsService.GetMediaStatsAsync();

    // Assert
    Assert.That(result.BookStats.TotalFormats, Is.EqualTo(3));
    Assert.That(result.BookStats.Formats, Contains.Item("Hardcover"));
    Assert.That(result.BookStats.Formats, Contains.Item("Paperback"));
    Assert.That(result.BookStats.Formats, Contains.Item("eBook"));
  }

  [Test]
  public async Task GetMediaStatsAsync_ShouldTrimWhitespace_FromBookFormats()
  {
    // Arrange
    var books = new List<Book>
    {
      _bookFaker.Clone().RuleFor(b => b.Format, "  Hardcover  ").Generate(),
      _bookFaker.Clone().RuleFor(b => b.Format, "Paperback  ").Generate()
    };

    _bookServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(books);
    _movieServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Movie>());
    _gameServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Game>());

    // Act
    var result = await _statsService.GetMediaStatsAsync();

    // Assert
    Assert.That(result.BookStats.Formats, Contains.Item("Hardcover"));
    Assert.That(result.BookStats.Formats, Contains.Item("Paperback"));
    Assert.That(result.BookStats.Formats.All(f => f == f.Trim()), Is.True);
  }

  [Test]
  public async Task GetMediaStatsAsync_ShouldExcludeEmptyBookFormats()
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
    _movieServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Movie>());
    _gameServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Game>());

    // Act
    var result = await _statsService.GetMediaStatsAsync();

    // Assert
    Assert.That(result.BookStats.TotalFormats, Is.EqualTo(2));
    Assert.That(result.BookStats.Formats, Contains.Item("Hardcover"));
    Assert.That(result.BookStats.Formats, Contains.Item("Paperback"));
  }

  [Test]
  public async Task GetMediaStatsAsync_ShouldCalculateMovieFormatsCorrectly()
  {
    // Arrange
    var movies = new List<Movie>
    {
      _movieFaker.Clone().RuleFor(m => m.Format, "DVD").Generate(),
      _movieFaker.Clone().RuleFor(m => m.Format, "Blu-ray").Generate(),
      _movieFaker.Clone().RuleFor(m => m.Format, "DVD").Generate(), // Duplicate
      _movieFaker.Clone().RuleFor(m => m.Format, "4K UHD").Generate()
    };

    _bookServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Book>());
    _movieServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(movies);
    _gameServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Game>());

    // Act
    var result = await _statsService.GetMediaStatsAsync();

    // Assert
    Assert.That(result.MovieStats.TotalFormats, Is.EqualTo(3));
    Assert.That(result.MovieStats.Formats, Contains.Item("DVD"));
    Assert.That(result.MovieStats.Formats, Contains.Item("Blu-ray"));
    Assert.That(result.MovieStats.Formats, Contains.Item("4K UHD"));
  }

  [Test]
  public async Task GetMediaStatsAsync_ShouldTrimWhitespace_FromMovieFormats()
  {
    // Arrange
    var movies = new List<Movie>
    {
      _movieFaker.Clone().RuleFor(m => m.Format, "  DVD  ").Generate(),
      _movieFaker.Clone().RuleFor(m => m.Format, "Blu-ray  ").Generate()
    };

    _bookServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Book>());
    _movieServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(movies);
    _gameServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Game>());

    // Act
    var result = await _statsService.GetMediaStatsAsync();

    // Assert
    Assert.That(result.MovieStats.Formats, Contains.Item("DVD"));
    Assert.That(result.MovieStats.Formats, Contains.Item("Blu-ray"));
    Assert.That(result.MovieStats.Formats.All(f => f == f.Trim()), Is.True);
  }

  [Test]
  public async Task GetMediaStatsAsync_ShouldExcludeEmptyMovieFormats()
  {
    // Arrange
    var movies = new List<Movie>
    {
      _movieFaker.Clone().RuleFor(m => m.Format, "DVD").Generate(),
      _movieFaker.Clone().RuleFor(m => m.Format, "").Generate(),
      _movieFaker.Clone().RuleFor(m => m.Format, "   ").Generate(),
      _movieFaker.Clone().RuleFor(m => m.Format, "Blu-ray").Generate()
    };

    _bookServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Book>());
    _movieServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(movies);
    _gameServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Game>());

    // Act
    var result = await _statsService.GetMediaStatsAsync();

    // Assert
    Assert.That(result.MovieStats.TotalFormats, Is.EqualTo(2));
    Assert.That(result.MovieStats.Formats, Contains.Item("DVD"));
    Assert.That(result.MovieStats.Formats, Contains.Item("Blu-ray"));
  }

  [Test]
  public async Task GetMediaStatsAsync_ShouldCalculateTotalPagesCorrectly()
  {
    // Arrange
    var books = new List<Book>
    {
      _bookFaker.Clone().RuleFor(b => b.Pages, 100).RuleFor(b => b.Authors, new List<string>()).Generate(),
      _bookFaker.Clone().RuleFor(b => b.Pages, 250).RuleFor(b => b.Authors, new List<string>()).Generate(),
      _bookFaker.Clone().RuleFor(b => b.Pages, 150).RuleFor(b => b.Authors, new List<string>()).Generate()
    };

    _bookServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(books);
    _movieServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Movie>());
    _gameServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Game>());

    // Act
    var result = await _statsService.GetMediaStatsAsync();

    // Assert
    Assert.That(result.BookStats.TotalPages, Is.EqualTo(500));
  }

  [Test]
  public async Task GetMediaStatsAsync_ShouldExcludeBooksWithoutPages_FromTotalPages()
  {
    // Arrange
    var books = new List<Book>
    {
      _bookFaker.Clone().RuleFor(b => b.Pages, 100).RuleFor(b => b.Authors, new List<string>()).Generate(),
      _bookFaker.Clone().RuleFor(b => b.Pages, (int?)null).RuleFor(b => b.Authors, new List<string>()).Generate(),
      _bookFaker.Clone().RuleFor(b => b.Pages, 200).RuleFor(b => b.Authors, new List<string>()).Generate()
    };

    _bookServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(books);
    _movieServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Movie>());
    _gameServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Game>());

    // Act
    var result = await _statsService.GetMediaStatsAsync();

    // Assert
    Assert.That(result.BookStats.TotalPages, Is.EqualTo(300));
  }

  [Test]
  public async Task GetMediaStatsAsync_ShouldCalculateUniqueAuthorsCorrectly()
  {
    // Arrange
    var books = new List<Book>
    {
      _bookFaker.Clone().RuleFor(b => b.Authors, new List<string> { "Stephen King", "J.K. Rowling" }).RuleFor(b => b.Pages, (int?)null).Generate(),
      _bookFaker.Clone().RuleFor(b => b.Authors, new List<string> { "Stephen King", "George R.R. Martin" }).RuleFor(b => b.Pages, (int?)null).Generate(),
      _bookFaker.Clone().RuleFor(b => b.Authors, new List<string> { "Brandon Sanderson" }).RuleFor(b => b.Pages, (int?)null).Generate()
    };

    _bookServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(books);
    _movieServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Movie>());
    _gameServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Game>());

    // Act
    var result = await _statsService.GetMediaStatsAsync();

    // Assert
    Assert.That(result.BookStats.UniqueAuthors, Is.EqualTo(4));
  }

  [Test]
  public async Task GetMediaStatsAsync_ShouldTrimWhitespace_FromAuthors()
  {
    // Arrange
    var books = new List<Book>
    {
      _bookFaker.Clone().RuleFor(b => b.Authors, new List<string> { "  Stephen King  ", "Stephen King" }).RuleFor(b => b.Pages, (int?)null).Generate()
    };

    _bookServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(books);
    _movieServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Movie>());
    _gameServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Game>());

    // Act
    var result = await _statsService.GetMediaStatsAsync();

    // Assert - Should count as one unique author after trimming
    Assert.That(result.BookStats.UniqueAuthors, Is.EqualTo(1));
  }

  [Test]
  public async Task GetMediaStatsAsync_ShouldExcludeBooksWithoutAuthors_FromUniqueAuthorsCount()
  {
    // Arrange
    var books = new List<Book>
    {
      _bookFaker.Clone().RuleFor(b => b.Authors, new List<string> { "Stephen King" }).RuleFor(b => b.Pages, (int?)null).Generate(),
      _bookFaker.Clone().RuleFor(b => b.Authors, new List<string>()).RuleFor(b => b.Pages, (int?)null).Generate(),
      _bookFaker.Clone().RuleFor(b => b.Authors, (List<string>)null).RuleFor(b => b.Pages, (int?)null).Generate()
    };

    _bookServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(books);
    _movieServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Movie>());
    _gameServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Game>());

    // Act
    var result = await _statsService.GetMediaStatsAsync();

    // Assert
    Assert.That(result.BookStats.UniqueAuthors, Is.EqualTo(1));
  }

  [Test]
  public async Task GetMediaStatsAsync_ShouldCalculateTvSeriesCountCorrectly()
  {
    // Arrange
    var movies = new List<Movie>
    {
      _movieFaker.Clone().RuleFor(m => m.IsTvSeries, true).Generate(),
      _movieFaker.Clone().RuleFor(m => m.IsTvSeries, false).Generate(),
      _movieFaker.Clone().RuleFor(m => m.IsTvSeries, true).Generate(),
      _movieFaker.Clone().RuleFor(m => m.IsTvSeries, false).Generate(),
      _movieFaker.Clone().RuleFor(m => m.IsTvSeries, true).Generate()
    };

    _bookServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Book>());
    _movieServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(movies);
    _gameServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Game>());

    // Act
    var result = await _statsService.GetMediaStatsAsync();

    // Assert
    Assert.That(result.MovieStats.TotalTvSeries, Is.EqualTo(3));
  }

  [Test]
  public async Task GetMediaStatsAsync_ShouldReturnZeroTvSeries_WhenAllMoviesAreNotTvSeries()
  {
    // Arrange
    var movies = new List<Movie>
    {
      _movieFaker.Clone().RuleFor(m => m.IsTvSeries, false).Generate(),
      _movieFaker.Clone().RuleFor(m => m.IsTvSeries, false).Generate(),
      _movieFaker.Clone().RuleFor(m => m.IsTvSeries, false).Generate()
    };

    _bookServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Book>());
    _movieServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(movies);
    _gameServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Game>());

    // Act
    var result = await _statsService.GetMediaStatsAsync();

    // Assert
    Assert.That(result.MovieStats.TotalTvSeries, Is.EqualTo(0));
  }

  [Test]
  public async Task GetMediaStatsAsync_ShouldFetchBooksAndMoviesInParallel()
  {
    // Arrange
    var books = _bookFaker.Generate(3);
    var movies = _movieFaker.Generate(3);

    _bookServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(books);
    _movieServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(movies);
    _gameServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Game>());

    // Act
    var result = await _statsService.GetMediaStatsAsync();

    // Assert - Both services should be called
    _bookServiceMock.Verify(s => s.GetListAsync(), Times.Once);
    _movieServiceMock.Verify(s => s.GetListAsync(), Times.Once);
    Assert.That(result, Is.Not.Null);
  }

  [Test]
  public async Task GetMediaStatsAsync_ShouldHandleLargeDatasets()
  {
    // Arrange
    var books = _bookFaker.Generate(1000);
    var movies = _movieFaker.Generate(1000);

    _bookServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(books);
    _movieServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(movies);
    _gameServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Game>());

    // Act
    var result = await _statsService.GetMediaStatsAsync();

    // Assert
    Assert.That(result.BookStats.Total, Is.EqualTo(1000));
    Assert.That(result.MovieStats.Total, Is.EqualTo(1000));
    Assert.That(result, Is.Not.Null);
  }

  [Test]
  public async Task GetMediaStatsAsync_ShouldReturnCompleteStats_WithMixedData()
  {
    // Arrange
    var books = new List<Book>
    {
      _bookFaker.Clone()
        .RuleFor(b => b.Format, "Hardcover")
        .RuleFor(b => b.Pages, 300)
        .RuleFor(b => b.Authors, new List<string> { "Author A", "Author B" })
        .Generate(),
      _bookFaker.Clone()
        .RuleFor(b => b.Format, "Paperback")
        .RuleFor(b => b.Pages, 250)
        .RuleFor(b => b.Authors, new List<string> { "Author B", "Author C" })
        .Generate(),
      _bookFaker.Clone()
        .RuleFor(b => b.Format, "Hardcover")
        .RuleFor(b => b.Pages, (int?)null)
        .RuleFor(b => b.Authors, new List<string>())
        .Generate()
    };

    var movies = new List<Movie>
    {
      _movieFaker.Clone()
        .RuleFor(m => m.Format, "DVD")
        .RuleFor(m => m.IsTvSeries, true)
        .Generate(),
      _movieFaker.Clone()
        .RuleFor(m => m.Format, "Blu-ray")
        .RuleFor(m => m.IsTvSeries, false)
        .Generate(),
      _movieFaker.Clone()
        .RuleFor(m => m.Format, "DVD")
        .RuleFor(m => m.IsTvSeries, true)
        .Generate()
    };

    _bookServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(books);
    _movieServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(movies);
    _gameServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Game>());

    // Act
    var result = await _statsService.GetMediaStatsAsync();

    // Assert
    Assert.That(result.BookStats.Total, Is.EqualTo(3));
    Assert.That(result.BookStats.TotalFormats, Is.EqualTo(2)); // Hardcover, Paperback
    Assert.That(result.BookStats.TotalPages, Is.EqualTo(550)); // 300 + 250, null excluded
    Assert.That(result.BookStats.UniqueAuthors, Is.EqualTo(3)); // Author A, B, C

    Assert.That(result.MovieStats.Total, Is.EqualTo(3));
    Assert.That(result.MovieStats.TotalFormats, Is.EqualTo(2)); // DVD, Blu-ray
    Assert.That(result.MovieStats.TotalTvSeries, Is.EqualTo(2));
  }

  #endregion

  #region Service Mock Verification Tests

  [Test]
  public async Task GetMediaStatsAsync_ShouldCallBookServiceGetListAsync_Once()
  {
    // Arrange
    _bookServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Book>());
    _movieServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Movie>());
    _gameServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Game>());

    // Act
    await _statsService.GetMediaStatsAsync();

    // Assert
    _bookServiceMock.Verify(s => s.GetListAsync(), Times.Once);
  }

  [Test]
  public async Task GetMediaStatsAsync_ShouldCallMovieServiceGetListAsync_Once()
  {
    // Arrange
    _bookServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Book>());
    _movieServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Movie>());
    _gameServiceMock.Setup(s => s.GetListAsync())
      .ReturnsAsync(new List<Game>());

    // Act
    await _statsService.GetMediaStatsAsync();

    // Assert
    _movieServiceMock.Verify(s => s.GetListAsync(), Times.Once);
  }

  [Test]
  public void StatsService_ShouldBeConstructed_WithValidServices()
  {
    // Arrange & Act
    var service = new StatsService(_bookServiceMock.Object, _movieServiceMock.Object, _gameServiceMock.Object);

    // Assert
    Assert.That(service, Is.Not.Null);
  }

  #endregion
}
