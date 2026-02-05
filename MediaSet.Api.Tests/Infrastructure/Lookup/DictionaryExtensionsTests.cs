using MediaSet.Api.Features.Entities.Models;
using NUnit.Framework;
using MediaSet.Api.Shared.Extensions;
using MediaSet.Api.Infrastructure.Lookup;
using System.Text.Json;
using System.Collections.Generic;

namespace MediaSet.Api.Tests.Infrastructure.Lookup;

[TestFixture]
public class DictionaryExtensionsTests
{
    [Test]
    public void ExtractStringFromData_WithStringValue_ReturnsString()
    {
        // Arrange
        var data = new Dictionary<string, object>
        {
            { "key", "value" }
        };

        // Act
        var result = data.ExtractStringFromData("key");

        // Assert
        Assert.That(result, Is.EqualTo("value"));
    }

    [Test]
    public void ExtractStringFromData_WithNullStringValue_ReturnsEmptyString()
    {
        // Arrange
        var data = new Dictionary<string, object>
        {
            { "key", null! }
        };

        // Act
        var result = data.ExtractStringFromData("key");

        // Assert
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void ExtractStringFromData_WithJsonElementString_ReturnsString()
    {
        // Arrange
        var jsonElement = JsonSerializer.Deserialize<JsonElement>("\"test value\"");
        var data = new Dictionary<string, object>
        {
            { "key", jsonElement }
        };

        // Act
        var result = data.ExtractStringFromData("key");

        // Assert
        Assert.That(result, Is.EqualTo("test value"));
    }

    [Test]
    public void ExtractStringFromData_WithJsonElementNull_ReturnsEmptyString()
    {
        // Arrange
        var jsonElement = JsonSerializer.Deserialize<JsonElement>("null");
        var data = new Dictionary<string, object>
        {
            { "key", jsonElement }
        };

        // Act
        var result = data.ExtractStringFromData("key");

        // Assert
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void ExtractStringFromData_WithIntegerValue_ReturnsStringRepresentation()
    {
        // Arrange
        var data = new Dictionary<string, object>
        {
            { "key", 42 }
        };

        // Act
        var result = data.ExtractStringFromData("key");

        // Assert
        Assert.That(result, Is.EqualTo("42"));
    }

    [Test]
    public void ExtractStringFromData_WithMissingKey_ReturnsEmptyString()
    {
        // Arrange
        var data = new Dictionary<string, object>();

        // Act
        var result = data.ExtractStringFromData("nonexistent");

        // Assert
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void ExtractAuthorsFromData_WithValidAuthors_ReturnsAuthorList()
    {
        // Arrange
        var json = """
        {
            "authors": [
                { "name": "Author One", "url": "/authors/OL1A" },
                { "name": "Author Two", "url": "/authors/OL2A" }
            ]
        }
        """;
        var jsonDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        // Act
        var result = jsonDoc!.ExtractAuthorsFromData();

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0].Name, Is.EqualTo("Author One"));
        Assert.That(result[0].Url, Is.EqualTo("/authors/OL1A"));
        Assert.That(result[1].Name, Is.EqualTo("Author Two"));
        Assert.That(result[1].Url, Is.EqualTo("/authors/OL2A"));
    }

    [Test]
    public void ExtractAuthorsFromData_WithAuthorsMissingUrl_ReturnsAuthorsWithEmptyUrl()
    {
        // Arrange
        var json = """
        {
            "authors": [
                { "name": "Author One" }
            ]
        }
        """;
        var jsonDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        // Act
        var result = jsonDoc!.ExtractAuthorsFromData();

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Name, Is.EqualTo("Author One"));
        Assert.That(result[0].Url, Is.EqualTo(string.Empty));
    }

    [Test]
    public void ExtractAuthorsFromData_WithAuthorsArrayEmpty_ReturnsEmptyList()
    {
        // Arrange
        var json = """
        {
            "authors": []
        }
        """;
        var jsonDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        // Act
        var result = jsonDoc!.ExtractAuthorsFromData();

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void ExtractAuthorsFromData_WithNoAuthorsKey_ReturnsEmptyList()
    {
        // Arrange
        var data = new Dictionary<string, object>();

        // Act
        var result = data.ExtractAuthorsFromData();

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void ExtractAuthorsFromData_WithEmptyAuthorName_SkipsAuthor()
    {
        // Arrange
        var json = """
        {
            "authors": [
                { "name": "", "url": "/authors/OL1A" },
                { "name": "Valid Author", "url": "/authors/OL2A" }
            ]
        }
        """;
        var jsonDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        // Act
        var result = jsonDoc!.ExtractAuthorsFromData();

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Name, Is.EqualTo("Valid Author"));
    }

    [Test]
    public void ExtractNumberOfPagesFromData_WithNumberOfPagesInt_ReturnsPages()
    {
        // Arrange
        var json = """
        {
            "number_of_pages": 350
        }
        """;
        var jsonDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        // Act
        var result = jsonDoc!.ExtractNumberOfPagesFromData();

        // Assert
        Assert.That(result, Is.EqualTo(350));
    }

    [Test]
    public void ExtractNumberOfPagesFromData_WithNumberOfPagesString_ReturnsPages()
    {
        // Arrange
        var json = """
        {
            "number_of_pages": "250"
        }
        """;
        var jsonDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        // Act
        var result = jsonDoc!.ExtractNumberOfPagesFromData();

        // Assert
        Assert.That(result, Is.EqualTo(250));
    }

    [Test]
    public void ExtractNumberOfPagesFromData_WithPaginationInt_ReturnsPages()
    {
        // Arrange
        var json = """
        {
            "pagination": 450
        }
        """;
        var jsonDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        // Act
        var result = jsonDoc!.ExtractNumberOfPagesFromData();

        // Assert
        Assert.That(result, Is.EqualTo(450));
    }

    [Test]
    public void ExtractNumberOfPagesFromData_WithBothPaginationAndNumberOfPages_FavorsNumberOfPages()
    {
        // Arrange
        var json = """
        {
            "number_of_pages": 300,
            "pagination": 400
        }
        """;
        var jsonDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        // Act
        var result = jsonDoc!.ExtractNumberOfPagesFromData();

        // Assert
        Assert.That(result, Is.EqualTo(300));
    }

    [Test]
    public void ExtractNumberOfPagesFromData_WithInvalidString_ReturnsZero()
    {
        // Arrange
        var json = """
        {
            "number_of_pages": "not a number"
        }
        """;
        var jsonDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        // Act
        var result = jsonDoc!.ExtractNumberOfPagesFromData();

        // Assert
        Assert.That(result, Is.EqualTo(0));
    }

    [Test]
    public void ExtractNumberOfPagesFromData_WithNoKeys_ReturnsZero()
    {
        // Arrange
        var data = new Dictionary<string, object>();

        // Act
        var result = data.ExtractNumberOfPagesFromData();

        // Assert
        Assert.That(result, Is.EqualTo(0));
    }

    [Test]
    public void ExtractPublishersFromData_WithStringArray_ReturnsPublisherList()
    {
        // Arrange
        var json = """
        {
            "publishers": ["Publisher One", "Publisher Two"]
        }
        """;
        var jsonDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        // Act
        var result = jsonDoc!.ExtractPublishersFromData();

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0].Name, Is.EqualTo("Publisher One"));
        Assert.That(result[1].Name, Is.EqualTo("Publisher Two"));
    }

    [Test]
    public void ExtractPublishersFromData_WithObjectArray_ReturnsPublisherList()
    {
        // Arrange
        var json = """
        {
            "publishers": [
                { "name": "Publisher One" },
                { "name": "Publisher Two" }
            ]
        }
        """;
        var jsonDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        // Act
        var result = jsonDoc!.ExtractPublishersFromData();

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0].Name, Is.EqualTo("Publisher One"));
        Assert.That(result[1].Name, Is.EqualTo("Publisher Two"));
    }

    [Test]
    public void ExtractPublishersFromData_WithEmptyArray_ReturnsEmptyList()
    {
        // Arrange
        var json = """
        {
            "publishers": []
        }
        """;
        var jsonDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        // Act
        var result = jsonDoc!.ExtractPublishersFromData();

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void ExtractPublishersFromData_WithNoPublishersKey_ReturnsEmptyList()
    {
        // Arrange
        var data = new Dictionary<string, object>();

        // Act
        var result = data.ExtractPublishersFromData();

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void ExtractPublishersFromData_WithEmptyPublisherName_SkipsPublisher()
    {
        // Arrange
        var json = """
        {
            "publishers": ["", "Valid Publisher"]
        }
        """;
        var jsonDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        // Act
        var result = jsonDoc!.ExtractPublishersFromData();

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Name, Is.EqualTo("Valid Publisher"));
    }

    [Test]
    public void ExtractSubjectsFromData_WithStringArray_ReturnsSubjectList()
    {
        // Arrange
        var json = """
        {
            "subjects": ["Fiction", "Science Fiction"]
        }
        """;
        var jsonDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        // Act
        var result = jsonDoc!.ExtractSubjectsFromData();

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0].Name, Is.EqualTo("Fiction"));
        Assert.That(result[0].Url, Is.EqualTo(string.Empty));
        Assert.That(result[1].Name, Is.EqualTo("Science Fiction"));
    }

    [Test]
    public void ExtractSubjectsFromData_WithObjectArray_ReturnsSubjectList()
    {
        // Arrange
        var json = """
        {
            "subjects": [
                { "name": "Fiction", "url": "/subjects/fiction" },
                { "name": "Adventure", "url": "/subjects/adventure" }
            ]
        }
        """;
        var jsonDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        // Act
        var result = jsonDoc!.ExtractSubjectsFromData();

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0].Name, Is.EqualTo("Fiction"));
        Assert.That(result[0].Url, Is.EqualTo("/subjects/fiction"));
        Assert.That(result[1].Name, Is.EqualTo("Adventure"));
        Assert.That(result[1].Url, Is.EqualTo("/subjects/adventure"));
    }

    [Test]
    public void ExtractSubjectsFromData_WithObjectsMissingUrl_ReturnsSubjectsWithEmptyUrl()
    {
        // Arrange
        var json = """
        {
            "subjects": [
                { "name": "Fiction" }
            ]
        }
        """;
        var jsonDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        // Act
        var result = jsonDoc!.ExtractSubjectsFromData();

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Name, Is.EqualTo("Fiction"));
        Assert.That(result[0].Url, Is.EqualTo(string.Empty));
    }

    [Test]
    public void ExtractSubjectsFromData_WithEmptyArray_ReturnsEmptyList()
    {
        // Arrange
        var json = """
        {
            "subjects": []
        }
        """;
        var jsonDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        // Act
        var result = jsonDoc!.ExtractSubjectsFromData();

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void ExtractSubjectsFromData_WithNoSubjectsKey_ReturnsEmptyList()
    {
        // Arrange
        var data = new Dictionary<string, object>();

        // Act
        var result = data.ExtractSubjectsFromData();

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void ExtractSubjectsFromData_WithEmptySubjectName_SkipsSubject()
    {
        // Arrange
        var json = """
        {
            "subjects": ["", "Valid Subject"]
        }
        """;
        var jsonDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        // Act
        var result = jsonDoc!.ExtractSubjectsFromData();

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Name, Is.EqualTo("Valid Subject"));
    }
}
