using System.Net;
using System.Net.Sockets;

namespace MediaSet.Api.Infrastructure.Storage;

/// <summary>
/// Guards against Server-Side Request Forgery (SSRF) by validating that image download
/// URLs resolve to public, non-reserved IP addresses.
/// </summary>
internal static class SsrfGuard
{
    /// <summary>
    /// DNS resolver function. Replaceable in tests to avoid real network calls.
    /// </summary>
    internal static Func<string, CancellationToken, Task<IPAddress[]>> DnsResolver =
        (host, ct) => Dns.GetHostAddressesAsync(host, ct);

    /// <summary>
    /// Returns true if the given IP address is in a private, loopback, or reserved range
    /// that should not be reachable from an image download request.
    /// Covers: loopback, RFC-1918 private ranges, link-local (169.254/16 incl. cloud metadata),
    /// IPv6 link-local, and IPv6 site-local.
    /// </summary>
    internal static bool IsPrivateOrReservedIp(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal;
        }

        var b = ip.GetAddressBytes();
        return b[0] == 10                                           // 10.0.0.0/8
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)           // 172.16.0.0/12
            || (b[0] == 192 && b[1] == 168)                         // 192.168.0.0/16
            || (b[0] == 169 && b[1] == 254);                        // 169.254.0.0/16 (link-local / cloud metadata)
    }

    /// <summary>
    /// Validates that the host in <paramref name="uri"/> does not point to a private or reserved
    /// IP address. For IP-literal hosts the check is direct; for hostnames all resolved addresses
    /// are checked.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the host resolves to a blocked address.</exception>
    internal static async Task ValidateUrlAsync(Uri uri, CancellationToken cancellationToken)
    {
        // IP literal host — no DNS lookup needed
        if (IPAddress.TryParse(uri.Host, out var literalIp))
        {
            if (IsPrivateOrReservedIp(literalIp))
            {
                throw new ArgumentException("Image URL targets a private or reserved address and is not allowed.");
            }

            return;
        }

        IPAddress[] addresses;
        try
        {
            addresses = await DnsResolver(uri.Host, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ArgumentException($"Unable to resolve hostname '{uri.Host}'.", ex);
        }

        if (addresses.Length == 0 || addresses.All(IsPrivateOrReservedIp))
        {
            throw new ArgumentException("Image URL targets a private or reserved address and is not allowed.");
        }
    }
}
