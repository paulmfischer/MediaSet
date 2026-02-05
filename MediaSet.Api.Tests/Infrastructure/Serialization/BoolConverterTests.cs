using NUnit.Framework;
using MediaSet.Api.Infrastructure.Serialization;

namespace MediaSet.Api.Tests.Infrastructure.Serialization;

[TestFixture]
public class BoolConverterTests
{
    private BoolConverter _converter = null!;

    [SetUp]
    public void Setup()
    {
        _converter = new BoolConverter();
    }

    [Test]
    public void Convert_ShouldReturnTrue_WhenValueIsTrue()
    {
        // Arrange
        var value = "true";

        // Act
        var result = _converter.Convert(value);

        // Assert
        Assert.That(result, Is.EqualTo(true));
    }

    [Test]
    public void Convert_ShouldReturnTrue_WhenValueIsTrueUpperCase()
    {
        // Arrange
        var value = "True";

        // Act
        var result = _converter.Convert(value);

        // Assert
        Assert.That(result, Is.EqualTo(true));
    }

    [Test]
    public void Convert_ShouldReturnFalse_WhenValueIsFalse()
    {
        // Arrange
        var value = "false";

        // Act
        var result = _converter.Convert(value);

        // Assert
        Assert.That(result, Is.EqualTo(false));
    }

    [Test]
    public void Convert_ShouldReturnFalse_WhenValueIsFalseUpperCase()
    {
        // Arrange
        var value = "False";

        // Act
        var result = _converter.Convert(value);

        // Assert
        Assert.That(result, Is.EqualTo(false));
    }

    [Test]
    public void Convert_ShouldReturnTrue_WhenValueIs1()
    {
        // Arrange
        var value = "1";

        // Act
        var result = _converter.Convert(value);

        // Assert
        Assert.That(result, Is.EqualTo(true));
    }

    [Test]
    public void Convert_ShouldReturnFalse_WhenValueIs0()
    {
        // Arrange
        var value = "0";

        // Act
        var result = _converter.Convert(value);

        // Assert
        Assert.That(result, Is.EqualTo(false));
    }

    [Test]
    public void Convert_ShouldReturnFalse_WhenValueIsInvalidString()
    {
        // Arrange
        var value = "invalid";

        // Act
        var result = _converter.Convert(value);

        // Assert
        Assert.That(result, Is.EqualTo(false));
    }

    [Test]
    public void Convert_ShouldReturnFalse_WhenValueIsEmptyString()
    {
        // Arrange
        var value = string.Empty;

        // Act
        var result = _converter.Convert(value);

        // Assert
        Assert.That(result, Is.EqualTo(false));
    }

    [Test]
    public void Convert_ShouldReturnFalse_WhenValueIs2()
    {
        // Arrange
        var value = "2";

        // Act
        var result = _converter.Convert(value);

        // Assert
        Assert.That(result, Is.EqualTo(false));
    }

    [Test]
    public void Convert_ShouldReturnObjectType()
    {
        // Arrange
        var value = "true";

        // Act
        var result = _converter.Convert(value);

        // Assert
        Assert.That(result, Is.TypeOf<bool>());
    }
}
