using System.Net;
using System.Net.Sockets;

namespace JevTicketRouter.Infrastructure.Decisions.Local;

/// <summary>The verdict on a candidate local-AI endpoint.</summary>
/// <param name="IsAllowed">Whether the engine may call this address.</param>
/// <param name="Reason">Why, in words safe to log and to show an operator.</param>
public readonly record struct EndpointVerdict(bool IsAllowed, string Reason);

/// <summary>
/// Decides whether a configured <c>LOCAL_AI_BASE_URL</c> is somewhere local mode is allowed to talk to.
/// <para>
/// Local mode exists so restricted data never leaves the organisation's network. A base URL pointing
/// at the public internet would defeat that silently, and a typo in a hostname is an easy way to do
/// it by accident — so anything that is not demonstrably loopback, private, or link-local is refused
/// by default. An administrator can override it deliberately, for the real case of a model gateway
/// on a routable corporate address, but never by accident.
/// </para>
/// </summary>
public static class LocalEndpointGuard
{
    /// <summary>Host suffixes treated as internal names.</summary>
    private static readonly string[] PrivateHostSuffixes =
    [
        ".local",
        ".internal",
        ".intranet",
        ".lan",
        ".home.arpa",
    ];

    /// <summary>Bare host names treated as the local machine.</summary>
    private static readonly string[] LoopbackHosts = ["localhost", "host.docker.internal"];

    /// <summary>
    /// Checks a base URL.
    /// </summary>
    /// <param name="baseUrl">The configured endpoint.</param>
    /// <param name="allowPublicEndpoint">
    /// The administrator override. When true, any well-formed absolute HTTP(S) URL is accepted.
    /// </param>
    public static EndpointVerdict Inspect(string? baseUrl, bool allowPublicEndpoint)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return new EndpointVerdict(false, "LOCAL_AI_BASE_URL is not configured.");
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return new EndpointVerdict(false, "LOCAL_AI_BASE_URL is not a valid absolute URL.");
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return new EndpointVerdict(false, $"LOCAL_AI_BASE_URL must use http or https, not '{uri.Scheme}'.");
        }

        if (allowPublicEndpoint)
        {
            return new EndpointVerdict(
                true,
                "Accepted because the administrator override LocalAi:AllowPublicEndpoint is enabled.");
        }

        if (IsPrivateHost(uri, out var reason))
        {
            return new EndpointVerdict(true, reason);
        }

        return new EndpointVerdict(
            false,
            $"LOCAL_AI_BASE_URL host '{uri.Host}' is not a loopback or private address. Local mode "
                + "refuses non-local endpoints so restricted data cannot leave the network. Set "
                + "LocalAi:AllowPublicEndpoint to true only if this address really is an internal gateway.");
    }

    private static bool IsPrivateHost(Uri uri, out string reason)
    {
        var host = uri.Host;

        if (uri.IsLoopback || LoopbackHosts.Contains(host, StringComparer.OrdinalIgnoreCase))
        {
            reason = $"Host '{host}' is loopback.";
            return true;
        }

        if (IPAddress.TryParse(host, out var address))
        {
            if (IsPrivateAddress(address))
            {
                reason = $"Host '{host}' is a private address.";
                return true;
            }

            reason = string.Empty;
            return false;
        }

        // A single-label name such as "ollama" or "model-gateway" can only resolve inside the local
        // network or a container network, so it is treated as internal.
        if (!host.Contains('.', StringComparison.Ordinal))
        {
            reason = $"Host '{host}' is a single-label internal name.";
            return true;
        }

        if (PrivateHostSuffixes.Any(suffix => host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
        {
            reason = $"Host '{host}' uses an internal domain suffix.";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    /// <summary>True for loopback, RFC 1918, carrier-grade NAT, link-local, and IPv6 unique-local.</summary>
    private static bool IsPrivateAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
            {
                return true;
            }

            // fc00::/7, IPv6 unique local addresses.
            var v6 = address.GetAddressBytes();
            if ((v6[0] & 0xFE) == 0xFC)
            {
                return true;
            }

            // An IPv4-mapped address such as ::ffff:10.0.0.1 is judged on the IPv4 it carries.
            return address.IsIPv4MappedToIPv6 && IsPrivateAddress(address.MapToIPv4());
        }

        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var bytes = address.GetAddressBytes();

        return bytes[0] switch
        {
            10 => true,                                     // 10.0.0.0/8
            127 => true,                                    // 127.0.0.0/8
            169 when bytes[1] == 254 => true,               // 169.254.0.0/16 link-local
            172 when bytes[1] >= 16 && bytes[1] <= 31 => true, // 172.16.0.0/12
            192 when bytes[1] == 168 => true,               // 192.168.0.0/16
            100 when bytes[1] >= 64 && bytes[1] <= 127 => true, // 100.64.0.0/10 carrier-grade NAT
            _ => false,
        };
    }
}
