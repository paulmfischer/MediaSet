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
    public void GetMaxDownloadSizeBytes_CalculatesCorrectly()
    {
        // Arrange
        var config = new ImageConfiguration { MaxDownloadSizeMb = 10 };

        // Act
        var result = config.GetMaxDownloadSizeBytes();

        // Assert
        Assert.That(result, Is.EqualTo(10 * 1024 * 1024));
    }

    [Test]
    public void GetAllowedMimeTypes_ParsesCorrectly()
    {
        // Arrange
        var config = new ImageConfiguration
        {
            AllowedMimeTypes = "image/jpeg, image/png, image/webp"
        };

        // Act
        var result = config.GetAllowedMimeTypes();

        // Assert
        var mimeTypeList = new List<string>(result);
        Assert.That(mimeTypeList.Count, Is.EqualTo(3));
        Assert.That(mimeTypeList, Does.Contain("image/jpeg"));
        Assert.That(mimeTypeList, Does.Contain("image/png"));
        Assert.That(mimeTypeList, Does.Contain("image/webp"));
    }
}
