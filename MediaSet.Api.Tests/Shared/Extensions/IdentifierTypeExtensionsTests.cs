using MediaSet.Api.Features.Entities.Models;
using NUnit.Framework;
using MediaSet.Api.Shared.Extensions;

namespace MediaSet.Api.Tests.Shared.Extensions;

[TestFixture]
public class IdentifierTypeExtensionsTests
{
    [Test]
    public void ToApiString_WithIsbn_ReturnsIsbnLowercase()
    {
        // Arrange
        var identifierType = IdentifierType.Isbn;

        // Act
        var result = identifierType.ToApiString();

        // Assert
        Assert.That(result, Is.EqualTo("isbn"));
    }

    [Test]
    public void ToApiString_WithLccn_ReturnsLccnLowercase()
    {
        // Arrange
        var identifierType = IdentifierType.Lccn;

        // Act
        var result = identifierType.ToApiString();

        // Assert
        Assert.That(result, Is.EqualTo("lccn"));
    }

    [Test]
    public void ToApiString_WithOclc_ReturnsOclcLowercase()
    {
        // Arrange
        var identifierType = IdentifierType.Oclc;

        // Act
        var result = identifierType.ToApiString();

        // Assert
        Assert.That(result, Is.EqualTo("oclc"));
    }

    [Test]
    public void ToApiString_WithOlid_ReturnsOlidLowercase()
    {
        // Arrange
        var identifierType = IdentifierType.Olid;

        // Act
        var result = identifierType.ToApiString();

        // Assert
        Assert.That(result, Is.EqualTo("olid"));
    }

    [Test]
    public void TryParseIdentifierType_WithValidIsbn_ReturnsTrueAndIdentifierType()
    {
        // Arrange
        var input = "isbn";

        // Act
        var success = IdentifierTypeExtensions.TryParseIdentifierType(input, out var result);

        // Assert
        Assert.That(success, Is.True);
        Assert.That(result, Is.EqualTo(IdentifierType.Isbn));
    }

    [Test]
    public void TryParseIdentifierType_WithValidLccn_ReturnsTrueAndIdentifierType()
    {
        // Arrange
        var input = "lccn";

        // Act
        var success = IdentifierTypeExtensions.TryParseIdentifierType(input, out var result);

        // Assert
        Assert.That(success, Is.True);
        Assert.That(result, Is.EqualTo(IdentifierType.Lccn));
    }

    [Test]
    public void TryParseIdentifierType_WithValidOclc_ReturnsTrueAndIdentifierType()
    {
        // Arrange
        var input = "oclc";

        // Act
        var success = IdentifierTypeExtensions.TryParseIdentifierType(input, out var result);

        // Assert
        Assert.That(success, Is.True);
        Assert.That(result, Is.EqualTo(IdentifierType.Oclc));
    }

    [Test]
    public void TryParseIdentifierType_WithValidOlid_ReturnsTrueAndIdentifierType()
    {
        // Arrange
        var input = "olid";

        // Act
        var success = IdentifierTypeExtensions.TryParseIdentifierType(input, out var result);

        // Assert
        Assert.That(success, Is.True);
        Assert.That(result, Is.EqualTo(IdentifierType.Olid));
    }

    [Test]
    public void TryParseIdentifierType_WithUppercaseInput_ReturnsTrueAndIdentifierType()
    {
        // Arrange
        var input = "ISBN";

        // Act
        var success = IdentifierTypeExtensions.TryParseIdentifierType(input, out var result);

        // Assert
        Assert.That(success, Is.True);
        Assert.That(result, Is.EqualTo(IdentifierType.Isbn));
    }

    [Test]
    public void TryParseIdentifierType_WithMixedCaseInput_ReturnsTrueAndIdentifierType()
    {
        // Arrange
        var input = "OcLc";

        // Act
        var success = IdentifierTypeExtensions.TryParseIdentifierType(input, out var result);

        // Assert
        Assert.That(success, Is.True);
        Assert.That(result, Is.EqualTo(IdentifierType.Oclc));
    }

    [Test]
    public void TryParseIdentifierType_WithInvalidInput_ReturnsFalse()
    {
        // Arrange
        var input = "invalid";

        // Act
        var success = IdentifierTypeExtensions.TryParseIdentifierType(input, out var result);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(result, Is.EqualTo(default(IdentifierType)));
    }

    [Test]
    public void TryParseIdentifierType_WithEmptyString_ReturnsFalse()
    {
        // Arrange
        var input = "";

        // Act
        var success = IdentifierTypeExtensions.TryParseIdentifierType(input, out var result);

        // Assert
        Assert.That(success, Is.False);
    }

    [Test]
    public void GetValidTypesString_ReturnsCommaSeparatedListOfTypes()
    {
        // Act
        var result = IdentifierTypeExtensions.GetValidTypesString();

        // Assert
        Assert.That(result, Is.Not.Empty);
        Assert.That(result, Does.Contain("isbn"));
        Assert.That(result, Does.Contain("lccn"));
        Assert.That(result, Does.Contain("oclc"));
        Assert.That(result, Does.Contain("olid"));
        Assert.That(result, Does.Contain(", "));
    }

    [Test]
    public void GetValidTypesString_ReturnsAllIdentifierTypes()
    {
        // Act
        var result = IdentifierTypeExtensions.GetValidTypesString();
        var parts = result.Split(", ");

        // Assert
        Assert.That(parts.Length, Is.EqualTo(6)); // Should have 6 identifier types (isbn, lccn, oclc, olid, upc, ean)
    }
}
