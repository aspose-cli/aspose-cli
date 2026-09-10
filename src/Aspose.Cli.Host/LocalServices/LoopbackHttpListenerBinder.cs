using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Host.LocalServices;

/// <summary>
/// Atomically starts an <see cref="HttpListener"/> on explicit loopback host
/// prefixes. A requested port of zero searches the dynamic range directly
/// with HTTP.sys instead of probing with a TCP socket first; on Windows the
/// TCP allocator can return ports reserved from HTTP.sys, so a probe is not
/// proof that an HTTP listener can bind the same number.
/// </summary>
internal static class LoopbackHttpListenerBinder
{
    private const int DynamicPortStart = 49_152;
    private const int DynamicPortCount = 16_384;
    private const int CandidateStep = 8_191;
    private const int MaxCandidateAttempts = 128;

    /// <summary>
    /// Starts a listener for the supplied loopback host names and returns the
    /// listener together with its resolved port. The caller owns the listener.
    /// </summary>
    /// <param name="requestedPort">An explicit port, or zero to select one.</param>
    /// <param name="hosts">
    /// Loopback host names to register, such as <c>127.0.0.1</c> and
    /// <c>localhost</c>.
    /// </param>
    /// <exception cref="CliException">
    /// Thrown with <c>LOOPBACK_PORT_IN_USE</c> when the explicit port or every
    /// sampled dynamic candidate cannot be bound.
    /// </exception>
    public static LoopbackHttpListenerBinding Start(
        int requestedPort,
        IReadOnlyList<string> hosts)
    {
        ArgumentNullException.ThrowIfNull(hosts);
        if (hosts.Count == 0)
        {
            throw new ArgumentException("At least one loopback host is required.", nameof(hosts));
        }

        if (requestedPort is < 0 or > 65_535)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedPort),
                "A loopback listener port is zero or between 1 and 65535.");
        }

        if (hosts.Any(static host =>
            !string.Equals(host, "127.0.0.1", StringComparison.Ordinal)
            && !string.Equals(
                host,
                "localhost",
                StringComparison.OrdinalIgnoreCase)
            && !string.Equals(host, "[::1]", StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Listener prefixes must name an explicit loopback host.",
                nameof(hosts));
        }

        if (requestedPort != 0)
        {
            return TryStart(requestedPort, hosts)
                ?? throw CliErrors.LoopbackPortInUse(requestedPort);
        }

        int start = RandomNumberGenerator.GetInt32(DynamicPortCount);
        int lastCandidate = DynamicPortStart + start;
        for (int attempt = 0; attempt < MaxCandidateAttempts; attempt++)
        {
            int offset = (start + (attempt * CandidateStep)) % DynamicPortCount;
            lastCandidate = DynamicPortStart + offset;
            LoopbackHttpListenerBinding? binding = TryStart(lastCandidate, hosts);
            if (binding is not null)
            {
                return binding;
            }
        }

        throw CliErrors.LoopbackPortInUse(lastCandidate);
    }

    private static LoopbackHttpListenerBinding? TryStart(
        int port,
        IReadOnlyList<string> hosts)
    {
        var listener = new HttpListener();
        foreach (string host in hosts)
        {
            listener.Prefixes.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"http://{host}:{port}/"));
        }

        try
        {
            listener.Start();
            return new LoopbackHttpListenerBinding(listener, port);
        }
        catch (Exception exception) when (
            IsAddressInUse(exception))
        {
            listener.Close();
            return null;
        }
        catch (Exception exception) when (
            exception is HttpListenerException or SocketException)
        {
            listener.Close();
            throw CliErrors.LoopbackListenerUnavailable(
                IsPermissionDenied(exception),
                exception);
        }
    }

    private static bool IsAddressInUse(Exception exception) =>
        exception is SocketException socket
            ? socket.SocketErrorCode == SocketError.AddressAlreadyInUse
            : exception is HttpListenerException listener
                && listener.ErrorCode is 32 or 48 or 98 or 183 or 10048;

    private static bool IsPermissionDenied(Exception exception) =>
        exception is SocketException socket
            ? socket.SocketErrorCode == SocketError.AccessDenied
            : exception is HttpListenerException listener
                && listener.ErrorCode is 5 or 13 or 10013;
}

/// <summary>An already-started loopback HTTP listener and its bound port.</summary>
/// <param name="Listener">The started listener, owned by the caller.</param>
/// <param name="Port">The explicit or selected TCP port.</param>
internal sealed record LoopbackHttpListenerBinding(HttpListener Listener, int Port);
