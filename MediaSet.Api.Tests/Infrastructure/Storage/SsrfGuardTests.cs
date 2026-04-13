using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Infrastructure.Storage;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Infrastructure.Storage;

[TestFixture]
public class SsrfGuardTests
{
    #region IsPrivateOrReservedIp Tests

    [TestCase("127.0.0.1")]
    [TestCase("127.0.0.2")]
    [TestCase("::1")]
    public void IsPrivateOrReservedIp_Loopback_ReturnsTrue(string ipString)
    {
        var ip = IPAddress.Parse(ipString);
        Assert.That(SsrfGuard.IsPrivateOrReservedIp(ip), Is.True);
    }

    [TestCase("10.0.0.1")]
    [TestCase("10.255.255.255")]
    public void IsPrivateOrReservedIp_Class10Private_ReturnsTrue(string ipString)
    {
        var ip = IPAddress.Parse(ipString);
        Assert.That(SsrfGuard.IsPrivateOrReservedIp(ip), Is.True);
    }

    [TestCase("172.16.0.1")]
    [TestCase("172.31.255.255")]
    public void IsPrivateOrReservedIp_Class172Private_ReturnsTrue(string ipString)
    {
        var ip = IPAddress.Parse(ipString);
        Assert.That(SsrfGuard.IsPrivateOrReservedIp(ip), Is.True);
    }

    [TestCase("192.168.0.1")]
    [TestCase("192.168.255.255")]
    public void IsPrivateOrReservedIp_Class192Private_ReturnsTrue(string ipString)
    {
        var ip = IPAddress.Parse(ipString);
        Assert.That(SsrfGuard.IsPrivateOrReservedIp(ip), Is.True);
    }

    [TestCase("169.254.0.1")]
    [TestCase("169.254.169.254")]  // AWS/Azure/GCP metadata endpoint
    public void IsPrivateOrReservedIp_LinkLocal_ReturnsTrue(string ipString)
    {
        var ip = IPAddress.Parse(ipString);
        Assert.That(SsrfGuard.IsPrivateOrReservedIp(ip), Is.True);
    }

    [TestCase("fe80::1")]   // IPv6 link-local
    public void IsPrivateOrReservedIp_IPv6LinkLocal_ReturnsTrue(string ipString)
    {
        var ip = IPAddress.Parse(ipString);
        Assert.That(SsrfGuard.IsPrivateOrReservedIp(ip), Is.True);
    }

    [TestCase("8.8.8.8")]
    [TestCase("1.1.1.1")]
    [TestCase("93.184.216.34")]
    [TestCase("172.15.255.255")]  // Just outside 172.16/12
    [TestCase("172.32.0.0")]      // Just outside 172.16/12 upper bound
    [TestCase("11.0.0.1")]        // Not 10.x.x.x
    public void IsPrivateOrReservedIp_PublicIp_ReturnsFalse(string ipString)
    {
        var ip = IPAddress.Parse(ipString);
        Assert.That(SsrfGuard.IsPrivateOrReservedIp(ip), Is.False);
    }

    #endregion

    #region ValidateUrlAsync Tests — IP literals (no DNS needed)

    [TestCase("http://127.0.0.1/image.jpg")]
    [TestCase("http://192.168.1.100/image.jpg")]
    [TestCase("http://10.0.0.1/image.jpg")]
    [TestCase("http://172.16.0.1/image.jpg")]
    [TestCase("http://169.254.169.254/latest/meta-data/")]
    public void ValidateUrlAsync_WithPrivateIpLiteralUrl_ThrowsArgumentException(string url)
    {
        var uri = new Uri(url);
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await SsrfGuard.ValidateUrlAsync(uri, CancellationToken.None));
        Assert.That(ex?.Message, Does.Contain("private or reserved"));
    }

    [TestCase("http://8.8.8.8/image.jpg")]
    [TestCase("https://1.1.1.1/image.jpg")]
    public async Task ValidateUrlAsync_WithPublicIpLiteralUrl_DoesNotThrow(string url)
    {
        var uri = new Uri(url);
        Assert.DoesNotThrowAsync(async () =>
            await SsrfGuard.ValidateUrlAsync(uri, CancellationToken.None));
        await Task.CompletedTask;
    }

    [Test]
    public void ValidateUrlAsync_WithHostnameThatResolvesToPrivateIp_ThrowsArgumentException()
    {
        var originalResolver = SsrfGuard.DnsResolver;
        try
        {
            SsrfGuard.DnsResolver = (_, _) => Task.FromResult(new[] { IPAddress.Parse("192.168.1.1") });
            var uri = new Uri("http://internal.example.com/image.jpg");

            var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
                await SsrfGuard.ValidateUrlAsync(uri, CancellationToken.None));
            Assert.That(ex?.Message, Does.Contain("private or reserved"));
        }
        finally
        {
            SsrfGuard.DnsResolver = originalResolver;
        }
    }

    [Test]
    public async Task ValidateUrlAsync_WithHostnameThatResolvesToPublicIp_DoesNotThrow()
    {
        var originalResolver = SsrfGuard.DnsResolver;
        try
        {
            SsrfGuard.DnsResolver = (_, _) => Task.FromResult(new[] { IPAddress.Parse("93.184.216.34") });
            var uri = new Uri("https://example.com/image.jpg");

            Assert.DoesNotThrowAsync(async () =>
                await SsrfGuard.ValidateUrlAsync(uri, CancellationToken.None));
            await Task.CompletedTask;
        }
        finally
        {
            SsrfGuard.DnsResolver = originalResolver;
        }
    }

    [Test]
    public void ValidateUrlAsync_WithHostnameThatResolvesToNoAddresses_ThrowsArgumentException()
    {
        var originalResolver = SsrfGuard.DnsResolver;
        try
        {
            SsrfGuard.DnsResolver = (_, _) => Task.FromResult(Array.Empty<IPAddress>());
            var uri = new Uri("http://nonexistent.example.com/image.jpg");

            var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
                await SsrfGuard.ValidateUrlAsync(uri, CancellationToken.None));
            Assert.That(ex?.Message, Does.Contain("private or reserved"));
        }
        finally
        {
            SsrfGuard.DnsResolver = originalResolver;
        }
    }

    #endregion
}
