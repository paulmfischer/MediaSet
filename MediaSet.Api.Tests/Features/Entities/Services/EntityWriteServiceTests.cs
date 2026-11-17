using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Features.Entities.Services;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Plex;
using MediaSet.Api.Infrastructure.Storage;
using MediaSet.Api.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Moq;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Entities.Services;

[TestFixture]
public class EntityWriteServiceTests
{
    private const string Id = "507f1f77bcf86cd799439011";
    private const string Url = "https://example.com/cover.jpg";

    private Mock<IEntityService<Game>> _entities = null!;
    private Mock<IImageService> _images = null!;
    private EntityWriteService<Game> _service = null!;
    private List<string> _order = null!;
    private List<(string? ImageUrl, string? Cover)> _saves = null!;

    [SetUp]
    public void Setup()
    {
        _order = [];
        _saves = [];
        _entities = new Mock<IEntityService<Game>>();
        _images = new Mock<IImageService>();
        _entities.Setup(e => e.CreateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()))
            .Callback(() => _order.Add("create")).Returns(Task.CompletedTask);
        _entities.Setup(e => e.UpdateAsync(It.IsAny<string>(), It.IsAny<Game>(), It.IsAny<CancellationToken>()))
            .Callback<string, Game, CancellationToken>((_, g, _) =>
            {
                _order.Add("update");
                _saves.Add((g.ImageUrl, g.CoverImage?.FilePath));
            })
            .ReturnsAsync(Mock.Of<ReplaceOneResult>());
        _images.Setup(i => i.DeleteImageAsync(It.IsAny<string>())).Callback<string>(_ => _order.Add("delete"));
        _service = new EntityWriteService<Game>(
            _entities.Object,
            new EntityImageCoordinator(_images.Object, NullLogger<EntityImageCoordinator>.Instance),
            NullLogger<EntityWriteService<Game>>.Instance);
    }

    private static Game NewGame(string? imageUrl = null, Image? cover = null) =>
        new() { Id = Id, Type = MediaTypes.Games, Title = "Title", Format = "Disc", ImageUrl = imageUrl, CoverImage = cover };

    private static Image StoredImage(string guid) => new()
    {
        Id = guid,
        FileName = $"{Id}-{guid}.jpg",
        FilePath = $"games/{Id}-{guid}.jpg",
        ContentType = "image/jpeg",
        FileSize = 1,
    };

    private static IFormFile File() => new FormFile(new MemoryStream([1]), 0, 1, "coverImage", "a.jpg");

    private void SetupUpload(Image image) =>
        _images.Setup(i => i.SaveImageAsync(It.IsAny<IFormFile>(), "games", Id, It.IsAny<CancellationToken>()))
            .Callback(() => _order.Add("store")).ReturnsAsync(image);

    private void SetupDownload(Image image) =>
        _images.Setup(i => i.DownloadAndSaveImageAsync(Url, "Games", Id, It.IsAny<CancellationToken>()))
            .Callback(() => _order.Add("store")).ReturnsAsync(image);

    private void SetupExisting(Game existing) =>
        _entities.Setup(e => e.GetAsync(Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

    [Test]
    public async Task CreateAsync_NoImage_WritesOnce()
    {
        var result = await _service.CreateAsync(NewGame(), ImageInput.None, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(EntityWriteStatus.Success));
        Assert.That(_order, Is.EqualTo((string[])["create"]));
    }

    [Test]
    public async Task CreateAsync_EmptyEntity_ReturnsEmptyEntityWithoutWriting()
    {
        var result = await _service.CreateAsync(new Game { Id = Id }, ImageInput.None, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(EntityWriteStatus.EmptyEntity));
        Assert.That(_order, Is.Empty);
    }

    [Test]
    public async Task CreateAsync_InvalidExternalId_ReturnsInvalidExternalIdWithoutWriting()
    {
        var movies = new Mock<IEntityService<Movie>>();
        var service = new EntityWriteService<Movie>(
            movies.Object,
            new EntityImageCoordinator(_images.Object, NullLogger<EntityImageCoordinator>.Instance),
            NullLogger<EntityWriteService<Movie>>.Instance);

        var result = await service.CreateAsync(
            new Movie { Id = Id, Title = "T", ExternalId = "tmdbid" }, ImageInput.None, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(EntityWriteStatus.InvalidExternalId));
        movies.Verify(m => m.CreateAsync(It.IsAny<Movie>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task CreateAsync_Upload_WritesTwiceAndClearsImageUrl()
    {
        SetupUpload(StoredImage("new"));

        var result = await _service.CreateAsync(NewGame("https://example.com/other.jpg"), ImageInput.Upload(File()), CancellationToken.None);

        Assert.That(result.ImageError, Is.Null);
        Assert.That(_order, Is.EqualTo((string[])["create", "store", "update"]));
        Assert.That(_saves.Single(), Is.EqualTo(((string?)null, (string?)StoredImage("new").FilePath)));
    }

    [Test]
    public async Task CreateAsync_UploadFails_StillClearsImageUrlAndReportsError()
    {
        _images.Setup(i => i.SaveImageAsync(It.IsAny<IFormFile>(), "games", Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("disk full"));

        var result = await _service.CreateAsync(NewGame("https://example.com/other.jpg"), ImageInput.Upload(File()), CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(EntityWriteStatus.Success));
        Assert.That(result.ImageError, Is.EqualTo("unknown"));
        Assert.That(_saves.Single(), Is.EqualTo(((string?)null, (string?)null)));
    }

    [Test]
    public async Task CreateAsync_UrlFails_DoesNotWriteAgain()
    {
        _images.Setup(i => i.DownloadAndSaveImageAsync(Url, "Games", Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("404"));

        var result = await _service.CreateAsync(NewGame(Url), ImageInput.FromEntityUrl(Url), CancellationToken.None);

        Assert.That(result.ImageError, Is.EqualTo("url_unreachable"));
        Assert.That(_order, Is.EqualTo((string[])["create"]));
    }

    [Test]
    public async Task UpdateAsync_IdMismatch_ReturnsIdMismatch()
    {
        var result = await _service.UpdateAsync("other", NewGame(), ImageInput.None, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(EntityWriteStatus.IdMismatch));
        Assert.That(_order, Is.Empty);
    }

    [Test]
    public async Task UpdateAsync_Missing_ReturnsNotFound()
    {
        _entities.Setup(e => e.GetAsync(Id, It.IsAny<CancellationToken>())).ReturnsAsync((Game?)null);

        var result = await _service.UpdateAsync(Id, NewGame(), ImageInput.None, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(EntityWriteStatus.NotFound));
        Assert.That(_order, Is.Empty);
    }

    [Test]
    public async Task UpdateAsync_Upload_DeletesReplacedFileAfterSave()
    {
        SetupExisting(NewGame(cover: StoredImage("old")));
        SetupUpload(StoredImage("new"));

        var result = await _service.UpdateAsync(Id, NewGame(), ImageInput.Upload(File()), CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(EntityWriteStatus.Success));
        Assert.That(_order, Is.EqualTo((string[])["store", "update", "delete"]));
        _images.Verify(i => i.DeleteImageAsync(StoredImage("old").FilePath), Times.Once);
    }

    [Test]
    public async Task UpdateAsync_StoreFails_KeepsPreviousCoverAndImageUrl()
    {
        SetupExisting(NewGame("https://example.com/old.jpg", StoredImage("old")));
        _images.Setup(i => i.DownloadAndSaveImageAsync(Url, "Games", Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("disk full"));

        var result = await _service.UpdateAsync(Id, NewGame(Url), ImageInput.FromEntityUrl(Url), CancellationToken.None);

        Assert.That(result.ImageError, Is.EqualTo("unknown"));
        Assert.That(_saves.Single(), Is.EqualTo(("https://example.com/old.jpg", StoredImage("old").FilePath)));
        _images.Verify(i => i.DeleteImageAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task UpdateAsync_EntityUrlUnchanged_DoesNotDownload()
    {
        SetupExisting(NewGame(Url, StoredImage("old")));

        await _service.UpdateAsync(Id, NewGame(Url, StoredImage("old")), ImageInput.FromEntityUrl(Url), CancellationToken.None);

        _images.Verify(i => i.DownloadAndSaveImageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _images.Verify(i => i.DeleteImageAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task UpdateAsync_FormUrlSameAsStored_StillDownloads()
    {
        SetupExisting(NewGame(Url, StoredImage("old")));
        SetupDownload(StoredImage("new"));

        await _service.UpdateAsync(Id, NewGame(Url), ImageInput.Download(Url), CancellationToken.None);

        _images.Verify(i => i.DownloadAndSaveImageAsync(Url, "Games", Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UpdateAsync_CoverCleared_DeletesOldFileAfterSave()
    {
        SetupExisting(NewGame(cover: StoredImage("old")));

        await _service.UpdateAsync(Id, NewGame(), ImageInput.None, CancellationToken.None);

        Assert.That(_order, Is.EqualTo((string[])["update", "delete"]));
    }

    [Test]
    public async Task UpdateAsync_DeleteOfReplacedFileThrows_StillSucceeds()
    {
        SetupExisting(NewGame(cover: StoredImage("old")));
        _images.Setup(i => i.DeleteImageAsync(It.IsAny<string>())).Throws(new UnauthorizedAccessException("denied"));

        var result = await _service.UpdateAsync(Id, NewGame(), ImageInput.None, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(EntityWriteStatus.Success));
    }

    [Test]
    public async Task DeleteImageAsync_ReportsEntityNotFoundAndNoImage()
    {
        _entities.Setup(e => e.GetAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync((Game?)null);
        SetupExisting(NewGame());

        Assert.That(await _service.DeleteImageAsync("missing", CancellationToken.None), Is.EqualTo(DeleteImageStatus.EntityNotFound));
        Assert.That(await _service.DeleteImageAsync(Id, CancellationToken.None), Is.EqualTo(DeleteImageStatus.NoImage));
    }

    [Test]
    public async Task DeleteAsync_ImageDeleteThrows_StillRemovesEntity()
    {
        SetupExisting(NewGame(cover: StoredImage("old")));
        _images.Setup(i => i.DeleteImageAsync(It.IsAny<string>())).Throws(new IOException("locked"));
        _entities.Setup(e => e.RemoveAsync(Id, It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<DeleteResult>());

        await _service.DeleteAsync(Id, CancellationToken.None);

        _entities.Verify(e => e.RemoveAsync(Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    private EntityWriteService<T> ServiceFor<T>(Mock<IEntityService<T>> entities) where T : IEntity
    {
        entities.Setup(e => e.UpdateAsync(It.IsAny<string>(), It.IsAny<T>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ReplaceOneResult>());
        return new EntityWriteService<T>(
            entities.Object,
            new EntityImageCoordinator(_images.Object, NullLogger<EntityImageCoordinator>.Instance),
            NullLogger<EntityWriteService<T>>.Instance);
    }

    [Test]
    public async Task CreateAsync_MovieAndMusic_DropClientSuppliedPlex()
    {
        var movies = new Mock<IEntityService<Movie>>();
        var music = new Mock<IEntityService<Music>>();
        var movie = new Movie { Id = Id, Title = "T", Plex = new object() };
        var album = new Music { Id = Id, Title = "T", Plex = new object() };

        await ServiceFor(movies).CreateAsync(movie, ImageInput.None, CancellationToken.None);
        await ServiceFor(music).CreateAsync(album, ImageInput.None, CancellationToken.None);

        Assert.That(movie.Plex, Is.Null);
        Assert.That(album.Plex, Is.Null);
    }

    [Test]
    public async Task UpdateAsync_MovieAndMusic_DropClientSuppliedPlex()
    {
        var movies = new Mock<IEntityService<Movie>>();
        var music = new Mock<IEntityService<Music>>();
        var movie = new Movie { Id = Id, Title = "T", Plex = new object() };
        var album = new Music { Id = Id, Title = "T", Plex = new object() };
        movies.Setup(e => e.GetAsync(Id, It.IsAny<CancellationToken>())).ReturnsAsync(new Movie { Id = Id, Title = "T" });
        music.Setup(e => e.GetAsync(Id, It.IsAny<CancellationToken>())).ReturnsAsync(new Music { Id = Id, Title = "T" });

        await ServiceFor(movies).UpdateAsync(Id, movie, ImageInput.None, CancellationToken.None);
        await ServiceFor(music).UpdateAsync(Id, album, ImageInput.None, CancellationToken.None);

        Assert.That(movie.Plex, Is.Null);
        Assert.That(album.Plex, Is.Null);
    }

    [Test]
    public async Task UpdateAsync_InvalidExternalId_ReturnsInvalidExternalIdWithoutWriting()
    {
        var movies = new Mock<IEntityService<Movie>>();

        var result = await ServiceFor(movies).UpdateAsync(
            Id, new Movie { Id = Id, Title = "T", ExternalId = "tmdbid" }, ImageInput.None, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(EntityWriteStatus.InvalidExternalId));
        movies.Verify(m => m.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        movies.Verify(m => m.UpdateAsync(It.IsAny<string>(), It.IsAny<Movie>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task CreateAsync_ExternalId_LowercasesSourceOnly()
    {
        var movie = new Movie { Id = Id, Title = "T", ExternalId = "TmdbID:Tt0111161" };

        var result = await ServiceFor(new Mock<IEntityService<Movie>>()).CreateAsync(movie, ImageInput.None, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(EntityWriteStatus.Success));
        Assert.That(result.Entity!.ExternalId, Is.EqualTo("tmdbid:Tt0111161"));
    }

    [Test]
    public async Task UpdateAsync_ExternalId_LowercasesSourceOnly()
    {
        var movies = new Mock<IEntityService<Movie>>();
        movies.Setup(e => e.GetAsync(Id, It.IsAny<CancellationToken>())).ReturnsAsync(new Movie { Id = Id, Title = "T" });
        var movie = new Movie { Id = Id, Title = "T", ExternalId = "TmdbID:Tt0111161" };

        var result = await ServiceFor(movies).UpdateAsync(Id, movie, ImageInput.None, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(EntityWriteStatus.Success));
        Assert.That(movie.ExternalId, Is.EqualTo("tmdbid:Tt0111161"));
    }

    [Test]
    public async Task UpdateAsync_UploadStored_ClearsImageUrl()
    {
        SetupExisting(NewGame("https://example.com/old.jpg", StoredImage("old")));
        SetupUpload(StoredImage("new"));

        await _service.UpdateAsync(Id, NewGame("https://example.com/other.jpg"), ImageInput.Upload(File()), CancellationToken.None);

        Assert.That(_saves.Single(), Is.EqualTo(((string?)null, (string?)StoredImage("new").FilePath)));
    }

    [Test]
    public async Task UpdateAsync_CoverClearedWithImageUrlUnchanged_ClearsCoverAndKeepsImageUrl()
    {
        SetupExisting(NewGame(Url, StoredImage("old")));

        await _service.UpdateAsync(Id, NewGame(Url), ImageInput.None, CancellationToken.None);

        Assert.That(_saves.Single(), Is.EqualTo(((string?)Url, (string?)null)));
        _images.Verify(i => i.DeleteImageAsync(StoredImage("old").FilePath), Times.Once);
    }

    private static (EntityWriteService<T> Service, Mock<IEntityService<T>> Entities) PlexService<T>(IPlexDataCleanup? cleanup, IImageService images) where T : IEntity
    {
        var entities = new Mock<IEntityService<T>>();
        entities.Setup(e => e.RemoveAsync(Id, It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<DeleteResult>());
        return (new EntityWriteService<T>(
            entities.Object,
            new EntityImageCoordinator(images, NullLogger<EntityImageCoordinator>.Instance),
            NullLogger<EntityWriteService<T>>.Instance,
            cleanup), entities);
    }

    [Test]
    public async Task DeleteAsync_Movie_DeletesPlexDataAfterEntity()
    {
        List<string> order = [];
        var cleanup = new Mock<IPlexDataCleanup>();
        cleanup.Setup(c => c.DeleteMovieDataAsync(Id, It.IsAny<CancellationToken>())).Callback(() => order.Add("plex")).Returns(Task.CompletedTask);
        var (service, entities) = PlexService<Movie>(cleanup.Object, _images.Object);
        entities.Setup(e => e.RemoveAsync(Id, It.IsAny<CancellationToken>())).Callback(() => order.Add("remove")).ReturnsAsync(Mock.Of<DeleteResult>());

        await service.DeleteAsync(Id, CancellationToken.None);

        Assert.That(order, Is.EqualTo((string[])["remove", "plex"]));
        cleanup.Verify(c => c.DeleteMusicDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task DeleteAsync_Music_DeletesMusicPlexData()
    {
        var cleanup = new Mock<IPlexDataCleanup>();
        var (service, _) = PlexService<Music>(cleanup.Object, _images.Object);

        await service.DeleteAsync(Id, CancellationToken.None);

        cleanup.Verify(c => c.DeleteMusicDataAsync(Id, It.IsAny<CancellationToken>()), Times.Once);
        cleanup.Verify(c => c.DeleteMovieDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task DeleteAsync_OtherEntityType_SkipsPlexCleanup()
    {
        var cleanup = new Mock<IPlexDataCleanup>();
        var (service, _) = PlexService<Game>(cleanup.Object, _images.Object);

        await service.DeleteAsync(Id, CancellationToken.None);

        cleanup.VerifyNoOtherCalls();
    }

    [Test]
    public async Task DeleteAsync_PlexNotRegistered_StillDeletes()
    {
        var (service, entities) = PlexService<Movie>(null, _images.Object);

        await service.DeleteAsync(Id, CancellationToken.None);

        entities.Verify(e => e.RemoveAsync(Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task DeleteAsync_PlexCleanupFails_DoesNotFailDelete()
    {
        var cleanup = new Mock<IPlexDataCleanup>();
        cleanup.Setup(c => c.DeleteMovieDataAsync(Id, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("mongo down"));
        var (service, _) = PlexService<Movie>(cleanup.Object, _images.Object);

        Assert.That(() => service.DeleteAsync(Id, CancellationToken.None), Throws.Nothing);
    }

    [Test]
    public void DeleteAsync_PlexCleanupCanceled_Propagates()
    {
        var cleanup = new Mock<IPlexDataCleanup>();
        cleanup.Setup(c => c.DeleteMovieDataAsync(Id, It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());
        var (service, _) = PlexService<Movie>(cleanup.Object, _images.Object);

        Assert.That(() => service.DeleteAsync(Id, CancellationToken.None), Throws.InstanceOf<OperationCanceledException>());
    }
}
