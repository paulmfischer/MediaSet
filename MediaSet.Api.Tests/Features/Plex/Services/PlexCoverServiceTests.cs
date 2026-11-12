using System;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Features.Plex.Services;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Plex;
using MediaSet.Api.Infrastructure.Storage;
using MediaSet.Api.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using SkiaSharp;

namespace MediaSet.Api.Tests.Features.Plex.Services;

[TestFixture]
public class PlexCoverServiceTests
{
    private const string MovieId = "m1";
    private const string RatingKey = "10";

    private Mock<IPlexClient> _client = null!;
    private Mock<IImageService> _images = null!;
    private Mock<IEntityService<Movie>> _movies = null!;
    private PlexCoverService _service = null!;
    private static readonly Image _saved = new() { Id = "i", FilePath = "movies/m1-i.jpg", ContentType = "image/jpeg" };

    [SetUp]
    public void Setup()
    {
        _client = new Mock<IPlexClient>();
        _images = new Mock<IImageService>();
        _movies = new Mock<IEntityService<Movie>>();
        _movies.Setup(m => m.GetAsync(MovieId, It.IsAny<CancellationToken>())).ReturnsAsync(() => new Movie { Id = MovieId, Title = "t" });
        _images.Setup(i => i.SaveImageBytesAsync(It.IsAny<byte[]>(), "Movies", MovieId, It.IsAny<CancellationToken>())).ReturnsAsync(_saved);
        _service = new PlexCoverService(_client.Object, _images.Object, _movies.Object, NullLogger<PlexCoverService>.Instance);
    }

    private static byte[] Poster(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.Red);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 75);
        return data.ToArray();
    }

    private void ServePoster(byte[]? data) =>
        _client.Setup(c => c.GetPosterAsync(RatingKey, It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(data);

    [Test]
    public async Task ApplyIfMissingAsync_MovieWithoutCoverAndLargePoster_SavesAndStoresCover()
    {
        ServePoster(Poster(1000, 1500));

        var applied = await _service.ApplyIfMissingAsync(MovieId, RatingKey, CancellationToken.None);

        Assert.That(applied, Is.True);
        _movies.Verify(m => m.UpdateAsync(MovieId, It.Is<Movie>(x => x.CoverImage == _saved), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ApplyIfMissingAsync_MovieAlreadyHasCover_DoesNotReadPlex()
    {
        _movies.Setup(m => m.GetAsync(MovieId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Movie { Id = MovieId, Title = "t", CoverImage = new Image { FilePath = "x" } });

        var applied = await _service.ApplyIfMissingAsync(MovieId, RatingKey, CancellationToken.None);

        Assert.That(applied, Is.False);
        _client.Verify(c => c.GetPosterAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ApplyIfMissingAsync_PlexHasNoPoster_ReturnsFalseAndSavesNothing()
    {
        ServePoster(null);

        var applied = await _service.ApplyIfMissingAsync(MovieId, RatingKey, CancellationToken.None);

        Assert.That(applied, Is.False);
        _movies.Verify(m => m.UpdateAsync(It.IsAny<string>(), It.IsAny<Movie>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ApplyIfMissingAsync_PosterIsThumbnailSized_IsSkipped()
    {
        ServePoster(Poster(PlexCoverService.MinWidth - 1, 600));

        var applied = await _service.ApplyIfMissingAsync(MovieId, RatingKey, CancellationToken.None);

        Assert.That(applied, Is.False);
        _images.Verify(i => i.SaveImageBytesAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ApplyIfMissingAsync_PosterNotAnImage_ReturnsFalse()
    {
        ServePoster([1, 2, 3]);

        var applied = await _service.ApplyIfMissingAsync(MovieId, RatingKey, CancellationToken.None);

        Assert.That(applied, Is.False);
    }

    [Test]
    public async Task ApplyIfMissingAsync_ImageSaveFails_ReturnsFalseWithoutThrowing()
    {
        ServePoster(Poster(1000, 1500));
        _images.Setup(i => i.SaveImageBytesAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(ImageFailure.Create(ImageFailureReason.UnsupportedFormat, "bad"));

        var applied = await _service.ApplyIfMissingAsync(MovieId, RatingKey, CancellationToken.None);

        Assert.That(applied, Is.False);
    }

    [Test]
    public async Task ApplyIfMissingAsync_CoverAddedWhileSaving_KeepsExistingCoverAndDeletesNewFile()
    {
        ServePoster(Poster(1000, 1500));
        var calls = 0;
        _movies.Setup(m => m.GetAsync(MovieId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++calls == 1
                ? new Movie { Id = MovieId, Title = "t" }
                : new Movie { Id = MovieId, Title = "t", CoverImage = new Image { FilePath = "other" } });

        var applied = await _service.ApplyIfMissingAsync(MovieId, RatingKey, CancellationToken.None);

        Assert.That(applied, Is.False);
        _images.Verify(i => i.DeleteImageAsync(_saved.FilePath), Times.Once);
        _movies.Verify(m => m.UpdateAsync(It.IsAny<string>(), It.IsAny<Movie>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ApplyIfMissingAsync_UpdateFails_Deletes_savedFileAndReturnsFalse()
    {
        ServePoster(Poster(1000, 1500));
        _movies.Setup(m => m.UpdateAsync(It.IsAny<string>(), It.IsAny<Movie>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException());

        var applied = await _service.ApplyIfMissingAsync(MovieId, RatingKey, CancellationToken.None);

        Assert.That(applied, Is.False);
        _images.Verify(i => i.DeleteImageAsync(_saved.FilePath), Times.Once);
    }
}
