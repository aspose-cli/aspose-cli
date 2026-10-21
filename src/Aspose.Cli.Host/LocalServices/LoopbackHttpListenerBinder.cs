using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Host.LocalServices;

/// <summary>
/// Atomically starts an <see cref="HttpListener"/> on <c>127.0.0.1</c>, the
/// one address and Host a local service answers and the one origin its pages
/// accept mutations from. A requested port of zero searches the dynamic range
/// directly with HTTP.sys instead of probing with a TCP socket first; on
/// Windows the TCP allocator can return ports reserved from HTTP.sys, so a
/// probe is not proof that an HTTP listener can bind the same number. A
/// candidate HTTP.sys refuses with access denied is skipped too: another
/// user's listener on a path beneath it (<c>http://127.0.0.1:P/x/</c>) holds
/// the port as surely as one on its root.
/// </summary>
internal static class LoopbackHttpListenerBinder
{
    private const int DynamicPortStart = 49_152;
    private const int DynamicPortCount = 16_384;
    private const int CandidateStep = 8_191;
    private const int MaxCandidateAttempts = 128;

    /// <summary>
    /// Starts a listener on <c>127.0.0.1</c> and returns it together with its
    /// resolved port. The caller owns the listener.
    /// </summary>
    /// <param name="requestedPort">An explicit port, or zero to select one.</param>
    /// <exception cref="CliException">
    /// Thrown with <c>LOOPBACK_PORT_IN_USE</c> when the explicit port or every
    /// sampled dynamic candidate is in use, and with
    /// <c>LOOPBACK_LISTENER_UNAVAILABLE</c> when the explicit port, or every
    /// candidate, is refused.
    /// </exception>
    public static LoopbackHttpListenerBinding Start(int requestedPort)
    {
        if (requestedPort is < 0 or > 65_535)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedPort),
                "A loopback listener port is zero or between 1 and 65535.");
        }

        if (requestedPort != 0)
        {
            return TryStart(requestedPort, out Exception? refused)
                ?? throw (refused is null
                    ? CliErrors.LoopbackPortInUse(requestedPort)
                    : CliErrors.LoopbackListenerUnavailable(permissionDenied: true, refused));
        }

        int start = RandomNumberGenerator.GetInt32(DynamicPortCount);
        return StartAny(Enumerable.Range(0, MaxCandidateAttempts)
            .Select(attempt => DynamicPortStart + ((start + (attempt * CandidateStep)) % DynamicPortCount)));
    }

    /// <summary>
    /// Starts a listener on the first of <paramref name="candidates"/> that is
    /// neither in use nor refused.
    /// </summary>
    internal static LoopbackHttpListenerBinding StartAny(IEnumerable<int> candidates)
    {
        int lastCandidate = 0;
        bool anyInUse = false;
        Exception? refused = null;
        foreach (int candidate in candidates)
        {
            lastCandidate = candidate;
            LoopbackHttpListenerBinding? binding = TryStart(candidate, out Exception? denied);
            if (binding is not null)
            {
                return binding;
            }
            anyInUse |= denied is null;
            refused ??= denied;
        }

        throw refused is not null && !anyInUse
            ? CliErrors.LoopbackListenerUnavailable(permissionDenied: true, refused)
            : CliErrors.LoopbackPortInUse(lastCandidate);
    }

    /// <summary>
    /// Starts a listener on <paramref name="port"/>, or returns null when the
    /// port is in use or, with <paramref name="refused"/> set, when HTTP.sys
    /// denies it.
    /// </summary>
    private static LoopbackHttpListenerBinding? TryStart(int port, out Exception? refused)
    {
        refused = null;
        var listener = new HttpListener();
        listener.Prefixes.Add(string.Create(
            CultureInfo.InvariantCulture,
            $"http://127.0.0.1:{port}/"));

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
            IsPermissionDenied(exception))
        {
            listener.Close();
            refused = exception;
            return null;
        }
        catch (Exception exception) when (
            exception is HttpListenerException or SocketException)
        {
            listener.Close();
            throw CliErrors.LoopbackListenerUnavailable(
                permissionDenied: false,
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
