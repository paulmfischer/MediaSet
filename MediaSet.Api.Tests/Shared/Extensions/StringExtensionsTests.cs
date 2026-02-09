using MediaSet.Api.Shared.Models;
using System;
using NUnit.Framework;
using MediaSet.Api.Shared.Extensions;
using System.Collections.Generic;

namespace MediaSet.Api.Tests.Shared.Extensions;

[TestFixture]
public class StringExtensionsTests
{
    [Test]
    public void CastTo_StringPropertyWithoutConverter_ReturnsString()
    {
        // Arrange
        var value = "Test String";
        var propertyInfo = typeof(Book).GetProperty(nameof(Book.Title))!;

        // Act
        var result = value.CastTo(propertyInfo);

        // Assert
        Assert.That(result, Is.EqualTo("Test String"));
    }

    [Test]
    public void CastTo_ListOfStringProperty_ReturnsListOfStrings()
    {
        // Arrange
        var value = "Author One | Author Two | Author Three";
        var propertyInfo = typeof(Book).GetProperty(nameof(Book.Authors))!;

        // Act
        var result = value.CastTo(propertyInfo);

        // Assert
        Assert.That(result, Is.TypeOf<List<string>>());
        var list = (List<string>)result!;
        Assert.That(list, Has.Count.EqualTo(3));
        Assert.That(list[0], Is.EqualTo("Author One"));
        Assert.That(list[1], Is.EqualTo("Author Two"));
        Assert.That(list[2], Is.EqualTo("Author Three"));
    }

    [Test]
    public void CastTo_ListOfStringPropertyWithSpaces_TrimsValues()
    {
        // Arrange
        var value = "  Genre One  |  Genre Two  ";
        var propertyInfo = typeof(Book).GetProperty(nameof(Book.Genres))!;

        // Act
        var result = value.CastTo(propertyInfo);

        // Assert
        var list = (List<string>)result!;
        Assert.That(list, Has.Count.EqualTo(2));
        Assert.That(list[0], Is.EqualTo("Genre One"));
        Assert.That(list[1], Is.EqualTo("Genre Two"));
    }

    [Test]
    public void CastTo_ListOfStringPropertyWithEmptyValues_FiltersOutEmpty()
    {
        // Arrange
        var value = "Genre One | | Genre Two";
        var propertyInfo = typeof(Book).GetProperty(nameof(Book.Genres))!;

        // Act
        var result = value.CastTo(propertyInfo);

        // Assert
        var list = (List<string>)result!;
        Assert.That(list, Has.Count.EqualTo(2));
        Assert.That(list[0], Is.EqualTo("Genre One"));
        Assert.That(list[1], Is.EqualTo("Genre Two"));
    }

    [Test]
    public void CastTo_ListOfStringPropertyWithSingleValue_ReturnsSingleItemList()
    {
        // Arrange
        var value = "Single Author";
        var propertyInfo = typeof(Book).GetProperty(nameof(Book.Authors))!;

        // Act
        var result = value.CastTo(propertyInfo);

        // Assert
        var list = (List<string>)result!;
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0], Is.EqualTo("Single Author"));
    }

    [Test]
    public void CastTo_NullableIntPropertyWithValidInteger_ReturnsInt()
    {
        // Arrange
        var value = "300";
        var propertyInfo = typeof(Book).GetProperty(nameof(Book.Pages))!;

        // Act
        var result = value.CastTo(propertyInfo);

        // Assert
        Assert.That(result, Is.EqualTo(300));
    }

    [Test]
    public void CastTo_NullableIntPropertyWithInvalidInteger_ReturnsNull()
    {
        // Arrange
        var value = "not a number";
        var propertyInfo = typeof(Book).GetProperty(nameof(Book.Pages))!;

        // Act
        var result = value.CastTo(propertyInfo);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void CastTo_NullableIntPropertyWithEmptyString_ReturnsNull()
    {
        // Arrange
        var value = "";
        var propertyInfo = typeof(Book).GetProperty(nameof(Book.Pages))!;

        // Act
        var result = value.CastTo(propertyInfo);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void CastTo_PropertyWithRuntimeConverter_UsesConverter()
    {
        // Arrange
        var value = "02:30"; // 2 hours 30 minutes in HH:MM format
        var propertyInfo = typeof(Movie).GetProperty(nameof(Movie.Runtime))!;

        // Act
        var result = value.CastTo(propertyInfo);

        // Assert
        Assert.That(result, Is.EqualTo(150)); // 2 hours 30 minutes = 150 minutes
    }

    [Test]
    public void CastTo_PropertyWithBoolConverter_UsesConverter()
    {
        // Arrange
        var value = "1";
        var propertyInfo = typeof(Movie).GetProperty(nameof(Movie.IsTvSeries))!;

        // Act
        var result = value.CastTo(propertyInfo);

        // Assert
        Assert.That(result, Is.EqualTo(true));
    }

    [Test]
    public void CastTo_PropertyWithBoolConverterNo_ReturnsFalse()
    {
        // Arrange
        var value = "0";
        var propertyInfo = typeof(Movie).GetProperty(nameof(Movie.IsTvSeries))!;

        // Act
        var result = value.CastTo(propertyInfo);

        // Assert
        Assert.That(result, Is.EqualTo(false));
    }

    [Test]
    public void CastTo_PropertyWithBoolConverterTrue_ReturnsTrue()
    {
        // Arrange
        var value = "true";
        var propertyInfo = typeof(Movie).GetProperty(nameof(Movie.IsTvSeries))!;

        // Act
        var result = value.CastTo(propertyInfo);

        // Assert
        Assert.That(result, Is.EqualTo(true));
    }

    [Test]
    public void CastTo_PropertyWithBoolConverterFalse_ReturnsFalse()
    {
        // Arrange
        var value = "false";
        var propertyInfo = typeof(Movie).GetProperty(nameof(Movie.IsTvSeries))!;

        // Act
        var result = value.CastTo(propertyInfo);

        // Assert
        Assert.That(result, Is.EqualTo(false));
    }

    [Test]
    public void CastTo_RuntimePropertyWithMinutesOnly_ReturnsMinutes()
    {
        // Arrange
        var value = "90"; // Single value without colon separator
        var propertyInfo = typeof(Movie).GetProperty(nameof(Movie.Runtime))!;

        // Act
        var result = value.CastTo(propertyInfo);

        // Assert
        Assert.That(result, Is.EqualTo(90)); // RuntimeConverter now correctly handles minutes-only format
    }

    [Test]
    public void CastTo_RuntimePropertyWithHoursAndMinutes_UsesConverter()
    {
        // Arrange
        var value = "02:00"; // 2 hours in HH:MM format
        var propertyInfo = typeof(Movie).GetProperty(nameof(Movie.Runtime))!;

        // Act
        var result = value.CastTo(propertyInfo);

        // Assert
        Assert.That(result, Is.EqualTo(120));
    }

    [Test]
    public void CastTo_RuntimePropertyWithShortFormat_UsesConverter()
    {
        // Arrange
        var value = "1:30"; // 1 hour 30 minutes in H:MM format
        var propertyInfo = typeof(Movie).GetProperty(nameof(Movie.Runtime))!;

        // Act
        var result = value.CastTo(propertyInfo);

        // Assert
        Assert.That(result, Is.EqualTo(90));
    }

    [Test]
    public void CastTo_RuntimePropertyWithInvalidFormat_ThrowsFormatException()
    {
        // Arrange
        var value = "invalid";
        var propertyInfo = typeof(Movie).GetProperty(nameof(Movie.Runtime))!;

        // Act & Assert
        Assert.Throws<FormatException>(() => value.CastTo(propertyInfo));
    }

    [Test]
    public void CastTo_ListPropertyWithOnlyWhitespace_ReturnsEmptyList()
    {
        // Arrange
        var value = "   |   |   ";
        var propertyInfo = typeof(Book).GetProperty(nameof(Book.Genres))!;

        // Act
        var result = value.CastTo(propertyInfo);

        // Assert
        var list = (List<string>)result!;
        Assert.That(list, Is.Empty);
    }
}
