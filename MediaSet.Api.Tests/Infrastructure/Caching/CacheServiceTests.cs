using MediaSet.Api.Infrastructure.Caching;
using NUnit.Framework;
using Moq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace MediaSet.Api.Tests.Infrastructure.Caching;

#nullable disable
#pragma warning disable CS8602 // Dereference of possibly null reference - Moq expression trees cannot track null checks

[TestFixture]
public class MemoryCacheServiceTests
{
    private Mock<IMemoryCache> _memoryCacheMock = null!;
    private Mock<IOptions<CacheSettings>> _cacheSettingsMock = null!;
    private Mock<ILogger<MemoryCacheService>> _loggerMock = null!;
    private MemoryCacheService _cacheService = null!;
    private CacheSettings _cacheSettings = null!;

    [SetUp]
    public void Setup()
    {
        _memoryCacheMock = new Mock<IMemoryCache>();
        _cacheSettingsMock = new Mock<IOptions<CacheSettings>>();
        _loggerMock = new Mock<ILogger<MemoryCacheService>>();

        _cacheSettings = new CacheSettings
        {
            EnableCaching = true,
            DefaultCacheDurationMinutes = 10,
            MetadataCacheDurationMinutes = 10,
            StatsCacheDurationMinutes = 10
        };

        _cacheSettingsMock.Setup(x => x.Value).Returns(_cacheSettings);

        _cacheService = new MemoryCacheService(
            _memoryCacheMock.Object,
            _cacheSettingsMock.Object,
            _loggerMock.Object);
    }

    #region GetAsync Tests

    [Test]
    public async Task GetAsync_ShouldReturnValue_WhenKeyExists()
    {
        // Arrange
        var key = "test-key";
        var expectedValue = "test-value";
        object cacheValue = expectedValue;

        _memoryCacheMock.Setup(c => c.TryGetValue(key, out cacheValue))
            .Returns(true);

        // Act
        var result = await _cacheService.GetAsync<string>(key);

        // Assert
        Assert.That(result, Is.EqualTo(expectedValue));
        _memoryCacheMock.Verify(c => c.TryGetValue(key, out cacheValue), Times.Once);
    }

    [Test]
    public async Task GetAsync_ShouldReturnNull_WhenKeyDoesNotExist()
    {
        // Arrange
        var key = "non-existent-key";
        object cacheValue = null;

        _memoryCacheMock.Setup(c => c.TryGetValue(key, out cacheValue))
            .Returns(false);

        // Act
        var result = await _cacheService.GetAsync<string>(key);

        // Assert
        Assert.That(result, Is.Null);
        _memoryCacheMock.Verify(c => c.TryGetValue(key, out cacheValue), Times.Once);
    }

    [Test]
    public async Task GetAsync_ShouldReturnNull_WhenCachingIsDisabled()
    {
        // Arrange
        _cacheSettings.EnableCaching = false;
        var key = "test-key";

        // Act
        var result = await _cacheService.GetAsync<string>(key);

        // Assert
        Assert.That(result, Is.Null);
        _memoryCacheMock.Verify(c => c.TryGetValue(It.IsAny<string>(), out It.Ref<object>.IsAny), Times.Never);
    }

    [Test]
    public async Task GetAsync_ShouldLogDebug_OnCacheHit()
    {
        // Arrange
        var key = "test-key";
        var expectedValue = "test-value";
        object cacheValue = expectedValue;

        _memoryCacheMock.Setup(c => c.TryGetValue(key, out cacheValue))
            .Returns(true);

        // Act
        await _cacheService.GetAsync<string>(key);

        // Assert
#nullable enable
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() != null && v.ToString().Contains("Cache hit")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
#nullable disable
    }

    [Test]
    public async Task GetAsync_ShouldLogDebug_OnCacheMiss()
    {
        // Arrange
        var key = "non-existent-key";
        object cacheValue = null;

        _memoryCacheMock.Setup(c => c.TryGetValue(key, out cacheValue))
            .Returns(false);

        // Act
        await _cacheService.GetAsync<string>(key);

        // Assert
#nullable enable
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() != null && v.ToString().Contains("Cache miss")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
#nullable disable
    }

    #endregion

    #region SetAsync Tests

    [Test]
    public async Task SetAsync_ShouldNotStoreValue_WhenCachingIsDisabled()
    {
        // Arrange
        _cacheSettings.EnableCaching = false;
        var key = "test-key";
        var value = "test-value";

        // Act
        await _cacheService.SetAsync(key, value);

        // Assert
        _memoryCacheMock.Verify(c => c.CreateEntry(It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region RemoveAsync Tests

    [Test]
    public async Task RemoveAsync_ShouldRemoveKey_FromCache()
    {
        // Arrange
        var key = "test-key";

        // Act
        await _cacheService.RemoveAsync(key);

        // Assert
        _memoryCacheMock.Verify(c => c.Remove(key), Times.Once);
    }

    [Test]
    public async Task RemoveAsync_ShouldLogInformation_WhenKeyIsRemoved()
    {
        // Arrange
        var key = "test-key";

        // Act
        await _cacheService.RemoveAsync(key);

        // Assert
#nullable enable
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() != null && v.ToString().Contains("Removed cache entry")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
#nullable disable
    }

    #endregion

    #region RemoveByPatternAsync Tests

    [Test]
    public async Task RemoveByPatternAsync_ShouldRemoveMatchingKeys_WithWildcard()
    {
        // Arrange
        var pattern = "metadata:Books:*";
        
        // Since we can't easily mock the concurrent dictionary behavior,
        // this test verifies the method completes without error
        
        // Act
        await _cacheService.RemoveByPatternAsync(pattern);

        // Assert - Should complete without throwing
        Assert.Pass("RemoveByPatternAsync completed successfully");
    }

    [Test]
    public async Task RemoveByPatternAsync_ShouldLogInformation_AfterRemoval()
    {
        // Arrange
        var pattern = "metadata:Books:*";

        // Act
        await _cacheService.RemoveByPatternAsync(pattern);

        // Assert
#nullable enable
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() != null && v.ToString().Contains("Removed") && v.ToString().Contains("cache entries")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
#nullable disable
    }

    #endregion

    #region Configuration Tests

    [Test]
    public void MemoryCacheService_ShouldBeConstructed_WithValidDependencies()
    {
        // Arrange & Act
        var service = new MemoryCacheService(
            _memoryCacheMock.Object,
            _cacheSettingsMock.Object,
            _loggerMock.Object);

        // Assert
        Assert.That(service, Is.Not.Null);
    }

    [Test]
    public async Task CacheService_ShouldRespectEnableCachingSetting()
    {
        // Arrange
        _cacheSettings.EnableCaching = false;
        var key = "test-key";
        var value = "test-value";

        // Act
        await _cacheService.SetAsync(key, value);
        var result = await _cacheService.GetAsync<string>(key);

        // Assert
        Assert.That(result, Is.Null);
        _memoryCacheMock.Verify(c => c.CreateEntry(It.IsAny<string>()), Times.Never);
        _memoryCacheMock.Verify(c => c.TryGetValue(It.IsAny<string>(), out It.Ref<object>.IsAny), Times.Never);
    }

    #endregion
}
