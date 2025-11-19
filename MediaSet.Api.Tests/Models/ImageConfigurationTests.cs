using NUnit.Framework;
using MediaSet.Api.Models;
using System.Collections.Generic;

namespace MediaSet.Api.Tests.Models;

[TestFixture]
public class ImageConfigurationTests
{
    [Test]
    public void GetMaxFileSizeBytes_CalculatesCorrectly()
    {
        // Arrange
        var config = new ImageConfiguration { MaxFileSizeMb = 5 };

        // Act
        var result = config.GetMaxFileSizeBytes();

        // Assert
        Assert.That(result, Is.EqualTo(5 * 1024 * 1024));
    }

    [Test]
    public void GetAllowedImageExtensions_ParsesCorrectly()
    {
        // Arrange
        var config = new ImageConfiguration
        {
            AllowedImageExtensions = "jpg, jpeg, png, webp"
        };

        // Act
        var result = config.GetAllowedImageExtensions();

        // Assert
        var extensionList = new List<string>(result);
        Assert.That(extensionList.Count, Is.EqualTo(4));
        Assert.That(extensionList, Does.Contain("jpg"));
        Assert.That(extensionList, Does.Contain("jpeg"));
        Assert.That(extensionList, Does.Contain("png"));
        Assert.That(extensionList, Does.Contain("webp"));
    }
}
