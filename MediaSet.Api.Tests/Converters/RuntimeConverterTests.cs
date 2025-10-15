using System;
using NUnit.Framework;
using MediaSet.Api.Converters;

namespace MediaSet.Api.Tests.Converters;

[TestFixture]
public class RuntimeConverterTests
{
    private RuntimeConverter _converter;

    [SetUp]
    public void Setup()
    {
        _converter = new RuntimeConverter();
    }

    [Test]
    public void Convert_ShouldReturnMinutes_WhenValueIsHoursAndMinutes()
    {
        // Arrange
        var value = "01:38";

        // Act
        var result = _converter.Convert(value);

        // Assert
        Assert.That(result, Is.EqualTo(98));
    }

    [Test]
    public void Convert_ShouldReturnMinutes_WhenValueIsTwoHours()
    {
        // Arrange
        var value = "02:00";

        // Act
        var result = _converter.Convert(value);

        // Assert
        Assert.That(result, Is.EqualTo(120));
    }

    [Test]
    public void Convert_ShouldReturnMinutes_WhenValueIsZeroHoursWithMinutes()
    {
        // Arrange
        var value = "00:45";

        // Act
        var result = _converter.Convert(value);

        // Assert
        Assert.That(result, Is.EqualTo(45));
    }

    [Test]
    public void Convert_ShouldReturnMinutes_WhenValueIsThreeHoursAndFifteenMinutes()
    {
        // Arrange
        var value = "03:15";

        // Act
        var result = _converter.Convert(value);

        // Assert
        Assert.That(result, Is.EqualTo(195));
    }

    [Test]
    public void Convert_ShouldReturnMinutes_WhenValueIsOnlyMinutes()
    {
        // Arrange
        var value = "45";

        // Act
        var result = _converter.Convert(value);

        // Assert
        Assert.That(result, Is.EqualTo(45));
    }

    [Test]
    public void Convert_ShouldReturnMinutes_WhenValueIsOnlyMinutesZero()
    {
        // Arrange
        var value = "0";

        // Act
        var result = _converter.Convert(value);

        // Assert
        Assert.That(result, Is.EqualTo(0));
    }

    [Test]
    public void Convert_ShouldReturnNull_WhenValueIsEmptyAfterSplit()
    {
        // Arrange
        var value = ":";

        // Act & Assert
        Assert.Throws<FormatException>(() => _converter.Convert(value));
    }

    [Test]
    public void Convert_ShouldThrowFormatException_WhenValueIsInvalid()
    {
        // Arrange
        var value = "invalid";

        // Act & Assert
        Assert.Throws<FormatException>(() => _converter.Convert(value));
    }

    [Test]
    public void Convert_ShouldReturnObjectType_WhenValueIsValid()
    {
        // Arrange
        var value = "01:30";

        // Act
        var result = _converter.Convert(value);

        // Assert
        Assert.That(result, Is.TypeOf<int>());
    }

    [Test]
    public void Convert_ShouldHandleSingleDigitHours()
    {
        // Arrange
        var value = "1:30";

        // Act
        var result = _converter.Convert(value);

        // Assert
        Assert.That(result, Is.EqualTo(90));
    }

    [Test]
    public void Convert_ShouldHandleLongRuntimes()
    {
        // Arrange
        var value = "10:25";

        // Act
        var result = _converter.Convert(value);

        // Assert
        Assert.That(result, Is.EqualTo(625));
    }
}
