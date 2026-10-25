using System;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Infrastructure.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using SkiaSharp;

namespace MediaSet.Api.Tests.Infrastructure.Storage;

[TestFixture]
public class ImageIngestionTests
{
    private const string MetadataMarker = "private-image-metadata-605";

    [TestCase(false, SKEncodedImageFormat.Jpeg, false)]
    [TestCase(false, SKEncodedImageFormat.Jpeg, true)]
    [TestCase(false, SKEncodedImageFormat.Png, false)]
    [TestCase(false, SKEncodedImageFormat.Png, true)]
    [TestCase(true, SKEncodedImageFormat.Jpeg, false)]
    [TestCase(true, SKEncodedImageFormat.Jpeg, true)]
    [TestCase(true, SKEncodedImageFormat.Png, false)]
    [TestCase(true, SKEncodedImageFormat.Png, true)]
    public async Task Ingest_ValidImage_NormalizesFormatAndValidatesPixels(
        bool download, SKEncodedImageFormat format, bool strip)
    {
        var bytes = CreateImage(format);
        var config = new ImageConfiguration { StripExifData = strip, AllowedImageExtensions = "jpeg,png,jpg" };
        byte[] saved = [];
        var storage = CreateStorage(data => saved = data);
        using var client = CreateClient(bytes);
        var service = CreateService(storage, config, client);

        var result = await Ingest(service, bytes, download);

        var jpeg = format == SKEncodedImageFormat.Jpeg;
        Assert.That(result.ContentType, Is.EqualTo(jpeg ? "image/jpeg" : "image/png"));
        Assert.That(result.FileName, Does.EndWith(jpeg ? ".jpeg" : ".png"));
        Assert.That(result.FileSize, Is.EqualTo(saved.Length));
        using var encoded = SKData.CreateCopy(saved);
        using var codec = SKCodec.Create(encoded);
        Assert.That(codec.EncodedFormat, Is.EqualTo(format));
        Assert.That(codec.Info.Width, Is.EqualTo(3));
        Assert.That(codec.Info.Height, Is.EqualTo(2));
        using var bitmap = SKBitmap.Decode(saved);
        if (!jpeg)
        {
            Assert.That(bitmap.GetPixel(0, 0).Alpha, Is.EqualTo(80));
        }
        if (!strip)
        {
            Assert.That(saved, Is.EqualTo(bytes));
        }
        storage.Verify(s => s.SaveImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase(false, false, 5, false)]
    [TestCase(false, true, 5, false)]
    [TestCase(true, false, 5, false)]
    [TestCase(true, true, 5, false)]
    [TestCase(false, false, 6, true)]
    [TestCase(false, true, 6, true)]
    [TestCase(true, false, 6, true)]
    [TestCase(true, true, 6, true)]
    [TestCase(false, true, 7, true)]
    [TestCase(true, true, 7, true)]
    public async Task Ingest_PixelLimit_EnforcedBeforeStorage(bool download, bool strip, long limit, bool accepted)
    {
        var bytes = CreateImage(SKEncodedImageFormat.Png);
        var storage = CreateStorage();
        using var client = CreateClient(bytes);
        var service = CreateService(storage, new ImageConfiguration { MaxPixelCount = limit, StripExifData = strip }, client);
        if (accepted)
        {
            await Ingest(service, bytes, download);
        }
        else
        {
            Assert.ThrowsAsync<ArgumentException>(() => Ingest(service, bytes, download));
            storage.Verify(s => s.SaveImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    [TestCase(false, "png")]
    [TestCase(true, "png")]
    [TestCase(false, "jpeg")]
    [TestCase(true, "jpeg")]
    public void Ingest_DisallowedDetectedFormat_RejectsRegardlessOfDeclaredMime(bool download, string allowed)
    {
        var bytes = CreateImage(allowed == "png" ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png);
        var storage = CreateStorage();
        using var client = CreateClient(bytes);
        var service = CreateService(storage, new ImageConfiguration { AllowedImageExtensions = allowed }, client);
        Assert.ThrowsAsync<ArgumentException>(() => Ingest(service, bytes, download));
        storage.Verify(s => s.SaveImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase(false, -1)]
    [TestCase(false, 0)]
    [TestCase(false, 1)]
    [TestCase(true, -1)]
    [TestCase(true, 0)]
    [TestCase(true, 1)]
    public async Task Ingest_ByteLimit_AcceptsEqualityAndRejectsExtraByte(bool download, int difference)
    {
        const int limit = 1024 * 1024;
        var original = CreateImage(SKEncodedImageFormat.Png);
        var bytes = new byte[limit + difference];
        original.CopyTo(bytes, 0);
        var storage = CreateStorage();
        using var client = new HttpClient(new UnknownLengthImageHandler(bytes));
        var service = CreateService(storage,
            new ImageConfiguration { MaxFileSizeMb = 1, StripExifData = false }, client);

        if (difference <= 0)
        {
            var result = await Ingest(service, bytes, download);
            Assert.That(result.FileSize, Is.EqualTo(bytes.Length));
            storage.Verify(s => s.SaveImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        else
        {
            // Under-report upload length to exercise the streaming limit, rather than the header precheck.
            using var stream = new MemoryStream(bytes);
            var file = new Mock<IFormFile>();
            file.SetupGet(f => f.Length).Returns(1);
            file.Setup(f => f.OpenReadStream()).Returns(stream);
            Assert.ThrowsAsync<ArgumentException>(() => download
                ? Ingest(service, bytes, true)
                : service.SaveImageAsync(file.Object, "books", "123", CancellationToken.None));
            storage.Verify(s => s.SaveImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void Ingest_InvalidOrUnsupportedBytes_NeverStores(bool download, bool strip)
    {
        byte[][] invalidImages =
        [
            [1, 2, 3, 4],
            Encoding.ASCII.GetBytes("GIF89a\x01\0\x01\0\0\0\0"),
            CreateImage(SKEncodedImageFormat.Png)[..40],
            CreateImage(SKEncodedImageFormat.Jpeg)[..100],
            CreateImage(SKEncodedImageFormat.Png)[..^12],
            CreateImage(SKEncodedImageFormat.Jpeg)[..^2],
            CreateImage(SKEncodedImageFormat.Webp)
        ];
        foreach (var bytes in invalidImages)
        {
            var storage = CreateStorage();
            using var client = CreateClient(bytes);
            var service = CreateService(storage, new ImageConfiguration { StripExifData = strip }, client);
            Assert.ThrowsAsync<ArgumentException>(() => Ingest(service, bytes, download));
            storage.Verify(s => s.SaveImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Ingest_MetadataStripping_RemovesExifIptcAndXmp(bool download)
    {
        var original = CreateImage(SKEncodedImageFormat.Jpeg);
        // Three APP segments exercise EXIF, IPTC and XMP retention/removal without copying metadata to output.
        var profiles = new[] { "Exif\0\0", "Photoshop 3.0\0", "http://ns.adobe.com/xap/1.0/\0" };
        using var stream = new MemoryStream();
        stream.Write(original.AsSpan(0, 2));
        foreach (var prefix in profiles)
        {
            var payload = Encoding.ASCII.GetBytes(prefix + MetadataMarker);
            stream.Write([0xff, (byte)(prefix.StartsWith("Photoshop", StringComparison.Ordinal) ? 0xed : 0xe1),
                (byte)((payload.Length + 2) >> 8), (byte)(payload.Length + 2)]);
            stream.Write(payload);
        }
        stream.Write(original.AsSpan(2));
        var bytes = stream.ToArray();
        foreach (var strip in new[] { false, true })
        {
            byte[] saved = [];
            var storage = CreateStorage(data => saved = data);
            using var client = CreateClient(bytes);
            var service = CreateService(storage, new ImageConfiguration { StripExifData = strip }, client);
            await Ingest(service, bytes, download);
            Assert.That(Encoding.ASCII.GetString(saved).Contains(MetadataMarker, StringComparison.Ordinal), Is.EqualTo(!strip));
            if (!strip)
            {
                Assert.That(saved, Is.EqualTo(bytes));
            }
        }
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Constructor_InvalidPixelLimit_RejectsConfiguration(long limit)
    {
        using var client = new HttpClient();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateService(CreateStorage(), new ImageConfiguration { MaxPixelCount = limit }, client));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Ingest_DimensionsWhoseProductOverflowsInt_RejectsBeforeAllocation(bool download)
    {
        var bytes = CreateImage(SKEncodedImageFormat.Png);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), 65_536);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), 65_536);
        uint crc = uint.MaxValue;
        foreach (var value in bytes.AsSpan(12, 17))
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320U : 0);
            }
        }
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(29), ~crc);
        var storage = CreateStorage();
        using var client = CreateClient(bytes);
        var service = CreateService(storage, new ImageConfiguration { MaxPixelCount = int.MaxValue }, client);
        Assert.ThrowsAsync<ArgumentException>(() => Ingest(service, bytes, download));
        storage.Verify(s => s.SaveImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void Ingest_Cancelled_PropagatesCancellationWithoutStorage()
    {
        var bytes = CreateImage(SKEncodedImageFormat.Png);
        var storage = CreateStorage();
        using var client = CreateClient(bytes);
        var service = CreateService(storage, new ImageConfiguration(), client);
        using var stream = new MemoryStream(bytes);
        var file = new FormFile(stream, 0, bytes.Length, "file", "cover.jpg");
        Assert.CatchAsync<OperationCanceledException>(() =>
            service.SaveImageAsync(file, "books", "123", new CancellationToken(true)));
        storage.Verify(s => s.SaveImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static byte[] CreateImage(SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(3, 2);
        bitmap.Erase(new SKColor(100, 200, 50, 80));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 75);
        return data.ToArray();
    }

    private static Mock<IImageStorageProvider> CreateStorage(Action<byte[]>? capture = null)
    {
        var storage = new Mock<IImageStorageProvider>();
        storage.Setup(s => s.SaveImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<byte[], string, CancellationToken>((data, _, _) => capture?.Invoke(data))
            .Returns(Task.CompletedTask);
        return storage;
    }

    private static ImageService CreateService(Mock<IImageStorageProvider> storage, ImageConfiguration config, HttpClient client)
        => new(storage.Object, Options.Create(config), client, NullLogger<ImageService>.Instance);

    private static async Task<Api.Shared.Models.Image> Ingest(ImageService service, byte[] bytes, bool download)
    {
        if (download)
        {
            return await service.DownloadAndSaveImageAsync("http://1.2.3.4/incorrect.txt", "books", "123", CancellationToken.None);
        }
        using var stream = new MemoryStream(bytes);
        var file = new FormFile(stream, 0, bytes.Length, "file", "incorrect.txt")
        {
            Headers = new HeaderDictionary(), ContentType = "application/octet-stream"
        };
        return await service.SaveImageAsync(file, "books", "123", CancellationToken.None);
    }

    private static HttpClient CreateClient(byte[] bytes) => new(new ImageHandler(bytes));

    private sealed class ImageHandler(byte[] bytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
            response.Content.Headers.ContentType = new("application/octet-stream");
            return Task.FromResult(response);
        }
    }

    private sealed class UnknownLengthImageHandler(byte[] bytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new UnknownLengthContent(bytes) });
    }

    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => stream.WriteAsync(bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
