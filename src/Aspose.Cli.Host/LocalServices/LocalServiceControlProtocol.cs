using System.IO.Pipes;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Host.LocalServices;

internal sealed record LocalServiceControlRequest(
    int Version,
    string RequestId,
    string Service,
    string InstanceId,
    string Nonce,
    string Token,
    string Command,
    string? Path = null);

internal sealed record LocalServiceControlResponse(
    int Version,
    string RequestId,
    string Service,
    string InstanceId,
    string Nonce,
    bool Ok,
    string? Message = null,
    JsonElement? Result = null);

internal sealed record LocalServiceControlEndpoint(
    string Service,
    string InstanceId)
{
    public string PipeName =>
        $"aspose-cli-{Service}-{Digest()}";

    public string UnixSocketPath => Path.Combine(
        PrivateUserStorage.EnsureDirectory(Path.Combine(
            PrivateUserStorage.TemporaryRoot(),
            "services",
            Service,
            "ipc")),
        Digest() + ".sock");

    private string Digest() => Convert.ToHexString(
            SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        Service + "\n" + InstanceId))
                .AsSpan(0, 16))
        .ToLowerInvariant();
}

/// <summary>
/// Owns the current-user named-pipe or Unix-socket listener lifecycle for one
/// versioned local-service control endpoint.
/// </summary>
internal sealed class LocalServiceControlServer : IDisposable
{
    internal const int ProtocolVersion =
        LocalServiceControlCodec.ProtocolVersion;
    internal const int MaximumPayloadBytes =
        LocalServiceControlCodec.MaximumPayloadBytes;

    private readonly LocalServiceControlEndpoint _endpoint;
    private readonly LocalServiceControlIdentity _identity;
    private readonly Func<
        LocalServiceControlRequest,
        LocalServiceControlResponse> _handler;
    private readonly Action<LocalServiceControlRequest>? _afterResponse;
    private readonly Func<Exception, string>? _describeFailure;
    private readonly TimeSpan _stageTimeout;
    private readonly CancellationTokenSource _shutdown = new();
    private Socket? _unixListener;
    private Task? _loop;
    private int _started;

    public LocalServiceControlServer(
        LocalServiceControlEndpoint endpoint,
        string nonce,
        string token,
        Func<
            LocalServiceControlRequest,
            LocalServiceControlResponse> handler,
        TimeSpan? stageTimeout = null,
        Action<LocalServiceControlRequest>? afterResponse = null,
        Func<Exception, string>? describeFailure = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(nonce);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentNullException.ThrowIfNull(handler);
        _endpoint = endpoint;
        _stageTimeout = stageTimeout
            ?? LocalServiceControlCodec.DefaultStageTimeout;
        if (_stageTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(stageTimeout));
        }

        _identity = new LocalServiceControlIdentity(
            endpoint,
            nonce,
            token);
        _handler = handler;
        _afterResponse = afterResponse;
        _describeFailure = describeFailure;
    }

    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            throw new InvalidOperationException(
                "The local-service control server is already started.");
        }

        if (OperatingSystem.IsWindows())
        {
            StartWindows();
            return;
        }

        StartUnix();
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        _unixListener?.Dispose();
        WakeWindowsListener();
        WaitForLoop();
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                TryDeleteUnixSocket(_endpoint.UnixSocketPath);
            }
        }
        finally
        {
            _shutdown.Dispose();
        }
    }

    public static LocalServiceControlResponse Send(
        LocalServiceControlEndpoint endpoint,
        string nonce,
        string token,
        string command,
        string? path = null,
        TimeSpan? timeout = null) =>
        LocalServiceControlClient.Send(
            endpoint,
            nonce,
            token,
            command,
            path,
            timeout);

    private void StartWindows()
    {
        var ready = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _loop = Task.Run(
            () => ListenWindows(_shutdown.Token, ready));
        ready.Task.Wait(_stageTimeout);
    }

    private void StartUnix()
    {
        string path = _endpoint.UnixSocketPath;
        RemoveStaleUnixSocket(path);
        var listener = new Socket(
            AddressFamily.Unix,
            SocketType.Stream,
            ProtocolType.Unspecified);
        try
        {
            listener.Bind(new UnixDomainSocketEndPoint(path));
            CurrentUserPipeSecurity.HardenPath(path);
            listener.Listen(backlog: 8);
            _unixListener = listener;
            _loop = Task.Run(
                () => ListenUnix(listener, _shutdown.Token));
        }
        catch
        {
            listener.Dispose();
            TryDeleteUnixSocket(path);
            throw;
        }
    }

    private async Task ListenWindows(
        CancellationToken cancellationToken,
        TaskCompletionSource ready)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using var pipe = new NamedPipeServerStream(
                _endpoint.PipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous
                    | PipeOptions.CurrentUserOnly);
            ready.TrySetResult();
            try
            {
                await pipe.WaitForConnectionAsync(
                    cancellationToken).ConfigureAwait(false);
                await HandleAsync(
                    pipe,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is OperationCanceledException
                    or IOException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }

    private async Task ListenUnix(
        Socket listener,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using Socket socket = await listener.AcceptAsync(
                    cancellationToken).ConfigureAwait(false);
                using var stream = new NetworkStream(
                    socket,
                    ownsSocket: false);
                await HandleAsync(
                    stream,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is OperationCanceledException
                    or SocketException
                    or ObjectDisposedException
                    or IOException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }

    private void WakeWindowsListener()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            using var wake = new NamedPipeClientStream(
                ".",
                _endpoint.PipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous);
            wake.Connect(100);
        }
        catch (Exception exception) when (
            exception is IOException or TimeoutException)
        {
        }
    }

    private void WaitForLoop()
    {
        try
        {
            _loop?.Wait(_stageTimeout + _stageTimeout);
        }
        catch (AggregateException exception)
            when (exception.InnerExceptions.All(static inner =>
                inner is OperationCanceledException
                    or IOException
                    or SocketException
                    or ObjectDisposedException))
        {
        }
    }

    private async Task HandleAsync(
        Stream stream,
        CancellationToken serverCancellation)
    {
        LocalServiceControlRequest? request = null;
        try
        {
            using var stage = CancellationTokenSource
                .CreateLinkedTokenSource(serverCancellation);
            stage.CancelAfter(_stageTimeout);
            request = await ReadRequestAsync(
                stream,
                stage.Token).ConfigureAwait(false);
            bool authorized = _identity.IsAuthorized(request);
            LocalServiceControlResponse response =
                Dispatch(request, authorized);
            await WriteResponseAsync(
                stream,
                response,
                stage.Token).ConfigureAwait(false);
            if (authorized)
            {
                _afterResponse?.Invoke(request!);
            }
        }
        catch (Exception exception) when (
            exception is IOException
                or JsonException
                or OperationCanceledException
                or InvalidDataException)
        {
        }
    }

    private LocalServiceControlResponse Dispatch(
        LocalServiceControlRequest? request,
        bool authorized)
    {
        if (!authorized)
        {
            return _identity.Response(
                request,
                ok: false,
                "The local-service control identity or token is invalid.");
        }
        if (string.Equals(
                request!.Command,
                "ping",
                StringComparison.Ordinal))
        {
            return _identity.Response(request, ok: true);
        }

        try
        {
            return _identity.Normalize(request, _handler(request));
        }
        catch (Exception exception)
        {
            string message = "The local service could not complete the request.";
            try { message = _describeFailure?.Invoke(exception) ?? message; }
            catch { /* Failure reporting must not terminate the control endpoint. */ }
            return _identity.Response(request, ok: false, message);
        }
    }

    private static async Task<LocalServiceControlRequest?> ReadRequestAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        byte[] payload =
            await LocalServiceControlCodec.ReadFrameAsync(
                stream,
                cancellationToken).ConfigureAwait(false);
        try
        {
            return LocalServiceControlCodec.DeserializeRequest(
                payload);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    private static async Task WriteResponseAsync(
        Stream stream,
        LocalServiceControlResponse response,
        CancellationToken cancellationToken)
    {
        byte[] payload = LocalServiceControlCodec.Serialize(
            response);
        try
        {
            await LocalServiceControlCodec.WriteFrameAsync(
                stream,
                payload,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    private static void RemoveStaleUnixSocket(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }
        if ((File.GetAttributes(path)
                & FileAttributes.ReparsePoint) != 0)
        {
            throw new UnauthorizedAccessException(
                "The local-service socket path is linked.");
        }

        PrivateUserStorage.ValidateUnixOwner(path);
        File.Delete(path);
    }

    private static void TryDeleteUnixSocket(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                PrivateUserStorage.ValidateUnixOwner(path);
                File.Delete(path);
            }
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException)
        {
        }
    }
}
