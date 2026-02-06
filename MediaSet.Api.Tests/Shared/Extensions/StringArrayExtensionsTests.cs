using MediaSet.Api.Shared.Models;
using MediaSet.Api.Features.Entities.Models;
using NUnit.Framework;
using MediaSet.Api.Shared.Extensions;
using System.Reflection;
using System.Collections.Generic;

namespace MediaSet.Api.Tests.Shared.Extensions;

[TestFixture]
public class StringArrayExtensionsTests
{
    [Test]
    public void GetValueByHeader_WithMatchingHeader_ReturnsValue()
    {
        // Arrange
        var headerFields = new List<string> { "Title", "ISBN", "Format" };
        var fields = new[] { "Test Title", "978-0-123456-78-9", "Hardcover" };
        var propertyInfo = typeof(Book).GetProperty(nameof(Book.Title))!;

        // Act
        var result = fields.GetValueByHeader<Book>(headerFields, propertyInfo);

        // Assert
        Assert.That(result, Is.EqualTo("Test Title"));
    }

    [Test]
    public void GetValueByHeader_WithCustomHeaderName_ReturnsValue()
    {
        // Arrange
        var headerFields = new List<string> { "Title", "Publication Date", "Format" };
        var fields = new[] { "Test Title", "2020-01-01", "Hardcover" };
        var propertyInfo = typeof(Book).GetProperty(nameof(Book.PublicationDate))!;

        // Act
        var result = fields.GetValueByHeader<Book>(headerFields, propertyInfo);

        // Assert
        Assert.That(result, Is.EqualTo("2020-01-01"));
    }

    [Test]
    public void GetValueByHeader_WithMissingHeader_ReturnsNull()
    {
        // Arrange
        var headerFields = new List<string> { "Title", "Format" };
        var fields = new[] { "Test Title", "Hardcover" };
        var propertyInfo = typeof(Book).GetProperty(nameof(Book.ISBN))!;

        // Act
        var result = fields.GetValueByHeader<Book>(headerFields, propertyInfo);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void GetValueByHeader_WithHeaderAtLastPosition_ReturnsValue()
    {
        // Arrange
        var headerFields = new List<string> { "Title", "ISBN", "Format" };
        var fields = new[] { "Test Title", "978-0-123456-78-9", "Hardcover" };
        var propertyInfo = typeof(Book).GetProperty(nameof(Book.Format))!;

        // Act
        var result = fields.GetValueByHeader<Book>(headerFields, propertyInfo);

        // Assert
        Assert.That(result, Is.EqualTo("Hardcover"));
    }

    [Test]
    public void GetValueByHeader_WithEmptyValue_ReturnsEmptyString()
    {
        // Arrange
        var headerFields = new List<string> { "Title", "ISBN", "Format" };
        var fields = new[] { "Test Title", "", "Hardcover" };
        var propertyInfo = typeof(Book).GetProperty(nameof(Book.ISBN))!;

        // Act
        var result = fields.GetValueByHeader<Book>(headerFields, propertyInfo);

        // Assert
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void GetValueByHeader_MovieWithCustomHeaderName_ReturnsValue()
    {
        // Arrange
        var headerFields = new List<string> { "Title", "Release Date", "Format" };
        var fields = new[] { "Test Movie", "2020-01-01", "Blu-ray" };
        var propertyInfo = typeof(Movie).GetProperty(nameof(Movie.ReleaseDate))!;

        // Act
        var result = fields.GetValueByHeader<Movie>(headerFields, propertyInfo);

        // Assert
        Assert.That(result, Is.EqualTo("2020-01-01"));
    }

    [Test]
    public void GetValueByHeader_WithAudienceRatingHeader_ReturnsValue()
    {
        // Arrange
        var headerFields = new List<string> { "Title", "Audience Rating", "Runtime" };
        var fields = new[] { "Test Movie", "PG-13", "120" };
        var propertyInfo = typeof(Movie).GetProperty(nameof(Movie.Rating))!;

        // Act
        var result = fields.GetValueByHeader<Movie>(headerFields, propertyInfo);

        // Assert
        Assert.That(result, Is.EqualTo("PG-13"));
    }

    [Test]
    public void GetValueByHeader_WithMultipleValuesInFields_ReturnsCorrectValue()
    {
        // Arrange
        var headerFields = new List<string> { "Title", "Author", "ISBN", "Format", "Genre" };
        var fields = new[] { "Test Book", "John Doe", "978-0-123456-78-9", "Hardcover", "Fiction" };
        var propertyInfo = typeof(Book).GetProperty(nameof(Book.ISBN))!;

        // Act
        var result = fields.GetValueByHeader<Book>(headerFields, propertyInfo);

        // Assert
        Assert.That(result, Is.EqualTo("978-0-123456-78-9"));
    }

    [Test]
    public void GetValueByHeader_WithHeaderAtFirstPosition_ReturnsValue()
    {
        // Arrange
        var headerFields = new List<string> { "Title", "ISBN", "Format" };
        var fields = new[] { "Test Title", "978-0-123456-78-9", "Hardcover" };
        var propertyInfo = typeof(Book).GetProperty(nameof(Book.Title))!;

        // Act
        var result = fields.GetValueByHeader<Book>(headerFields, propertyInfo);

        // Assert
        Assert.That(result, Is.EqualTo("Test Title"));
    }
}
