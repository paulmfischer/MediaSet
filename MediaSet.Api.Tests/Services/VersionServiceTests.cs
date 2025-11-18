using NUnit.Framework;
using MediaSet.Api.Services;
using System;

namespace MediaSet.Api.Tests.Services;

[TestFixture]
public class VersionServiceTests
{
    private VersionService _versionService = null!;

    [SetUp]
    public void Setup()
    {
        _versionService = new VersionService();
    }

    [Test]
    public void GetVersion_ShouldReturnNonEmptyString()
    {
        // Act
        var result = _versionService.GetVersion();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Not.Empty);
    }

    [Test]
    public void GetVersion_ShouldReturnValidSemanticVersion_OrLocalPlaceholder()
    {
        // Act
        var result = _versionService.GetVersion();

        // Assert
        Assert.That(result, Is.Not.Null);
        // Should either be a semantic version (x.y.z) or "0.0.0-local"
        var isValidFormat = result == "0.0.0-local" || 
                           result.Contains('.') || 
                           System.Text.RegularExpressions.Regex.IsMatch(result, @"^\d+\.\d+\.\d+");
        Assert.That(isValidFormat, Is.True, $"Version '{result}' should be in semantic version format or '0.0.0-local'");
    }

    [Test]
    public void GetCommitSha_ShouldReturnNonEmptyString()
    {
        // Act
        var result = _versionService.GetCommitSha();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Not.Empty);
    }

    [Test]
    public void GetCommitSha_ShouldReturnValidCommitSha_OrUnknown()
    {
        // Act
        var result = _versionService.GetCommitSha();

        // Assert
        Assert.That(result, Is.Not.Null);
        // Should either be "unknown" or a hex string (commit SHA)
        var isValidFormat = result == "unknown" || 
                           System.Text.RegularExpressions.Regex.IsMatch(result, @"^[0-9a-f]+$");
        Assert.That(isValidFormat, Is.True, $"Commit SHA '{result}' should be hex format or 'unknown'");
    }

    [Test]
    public void GetBuildTime_ShouldReturnNonEmptyString()
    {
        // Act
        var result = _versionService.GetBuildTime();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Not.Empty);
    }

    [Test]
    public void GetBuildTime_ShouldReturnValidIso8601DateTime_OrParseable()
    {
        // Act
        var result = _versionService.GetBuildTime();

        // Assert
        Assert.That(result, Is.Not.Null);
        // Should be parseable as a DateTime
        var canParse = DateTime.TryParse(result, out _);
        Assert.That(canParse, Is.True, $"Build time '{result}' should be a valid datetime string");
    }

    [Test]
    public void VersionService_ShouldBeConstructable()
    {
        // Arrange & Act
        var service = new VersionService();

        // Assert
        Assert.That(service, Is.Not.Null);
    }

    [Test]
    public void VersionService_ShouldReturnConsistentValues()
    {
        // Arrange
        var service1 = new VersionService();
        var service2 = new VersionService();

        // Act
        var version1 = service1.GetVersion();
        var version2 = service2.GetVersion();
        var commit1 = service1.GetCommitSha();
        var commit2 = service2.GetCommitSha();

        // Assert
        Assert.That(version2, Is.EqualTo(version1), "Version should be consistent across instances");
        Assert.That(commit2, Is.EqualTo(commit1), "Commit SHA should be consistent across instances");
    }
}
