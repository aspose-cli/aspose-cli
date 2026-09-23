using System.Net;

namespace Aspose.Cli.Host.LocalServices;

/// <summary>
/// Shared loopback, host, and CSRF checks for local HTTP services.
/// </summary>
internal sealed class LocalHttpRequestSecurity
{
    internal const string CsrfHeader = "X-CSRF-Token";

    private readonly string _csrf;
    public LocalHttpRequestSecurity(string csrf)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csrf);
        _csrf = csrf;
    }

    public static bool IsRequestAllowed(
        HttpListenerRequest request,
        int expectedPort)
    {
        ArgumentNullException.ThrowIfNull(request);
        return IsLoopbackRemote(request.RemoteEndPoint)
            && IsExactLoopbackHost(request.UserHostName, expectedPort);
    }

    public bool IsMutationAuthorized(
        HttpListenerRequest request,
        int expectedPort)
    {
        return string.Equals(
                request.Headers["Origin"],
                "http://" + Authority(expectedPort),
                StringComparison.Ordinal)
            && SecretText.FixedEquals(
                request.Headers[CsrfHeader],
                _csrf);
    }

    internal static bool IsLoopbackRemote(IPEndPoint? remoteEndPoint) =>
        remoteEndPoint is not null
        && IPAddress.IsLoopback(remoteEndPoint.Address);

    /// <summary>
    /// The one Host a local service answers: the address it is bound to, as
    /// a byte-exact string. Parsing the header as a URI would accept user
    /// information, trailing dots or default ports that name the same host.
    /// </summary>
    internal static bool IsExactLoopbackHost(
        string? hostHeader,
        int expectedPort) =>
        string.Equals(hostHeader, Authority(expectedPort), StringComparison.Ordinal);

    /// <summary>The Host and Origin authority a browser sends for the bound address.</summary>
    private static string Authority(int port) =>
        port == 80
            ? "127.0.0.1"
            : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"127.0.0.1:{port}");

    internal static string RandomToken() =>
        Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .ToLowerInvariant();
}
