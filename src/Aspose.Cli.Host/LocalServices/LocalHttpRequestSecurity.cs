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
        string expectedOrigin =
            $"http://127.0.0.1:{expectedPort}";
        return string.Equals(
                request.Headers["Origin"],
                expectedOrigin,
                StringComparison.Ordinal)
            && SecretText.FixedEquals(
                request.Headers[CsrfHeader],
                _csrf);
    }

    internal static bool IsLoopbackRemote(IPEndPoint? remoteEndPoint) =>
        remoteEndPoint is not null
        && IPAddress.IsLoopback(remoteEndPoint.Address);

    internal static bool IsExactLoopbackHost(
        string? hostHeader,
        int expectedPort)
    {
        if (string.IsNullOrWhiteSpace(hostHeader)
            || !Uri.TryCreate(
                "http://" + hostHeader,
                UriKind.Absolute,
                out Uri? uri)
            || uri.Port != expectedPort)
        {
            return false;
        }

        string host = uri.Host.Trim('[', ']');
        return string.Equals(host, "127.0.0.1", StringComparison.Ordinal)
            || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "::1", StringComparison.Ordinal);
    }

    internal static string RandomToken() =>
        Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .ToLowerInvariant();
}
