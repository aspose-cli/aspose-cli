using System.IO.Pipes;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace Aspose.Cli.Host.LocalServices;

/// <summary>Connects to and validates one local-service control exchange.</summary>
internal static class LocalServiceControlClient
{
    public static LocalServiceControlResponse Send(
        LocalServiceControlEndpoint endpoint,
        string nonce,
        string token,
        string command,
        string? path,
        TimeSpan? timeout,
        string? payload = null)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                return SendOnce(
                    endpoint,
                    nonce,
                    token,
                    command,
                    path,
                    timeout,
                    payload);
            }
            catch (Exception exception) when (
                attempt == 0
                && exception is IOException or SocketException)
            {
                Thread.Sleep(25);
            }
        }
    }

    private static LocalServiceControlResponse SendOnce(
        LocalServiceControlEndpoint endpoint,
        string nonce,
        string token,
        string command,
        string? path,
        TimeSpan? timeout,
        string? payload)
    {
        TimeSpan budget = timeout
            ?? LocalServiceControlCodec.DefaultStageTimeout;
        using var cancellation =
            new CancellationTokenSource(budget);
        using Stream stream = ConnectAsync(
            endpoint,
            cancellation.Token).GetAwaiter().GetResult();
        string requestId = Guid.NewGuid().ToString("N");
        var request = new LocalServiceControlRequest(
            LocalServiceControlCodec.ProtocolVersion,
            requestId,
            endpoint.Service,
            endpoint.InstanceId,
            nonce,
            token,
            command,
            path,
            payload);
        byte[] requestPayload = LocalServiceControlCodec.Serialize(request);
        try
        {
            LocalServiceControlCodec.WriteFrameAsync(
                stream,
                requestPayload,
                cancellation.Token).GetAwaiter().GetResult();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(requestPayload);
        }

        byte[] responsePayload =
            LocalServiceControlCodec.ReadFrameAsync(
                stream,
                cancellation.Token).GetAwaiter().GetResult();
        try
        {
            LocalServiceControlResponse response =
                LocalServiceControlCodec.DeserializeResponse(
                    responsePayload);
            LocalServiceControlIdentity.ValidateResponse(
                endpoint,
                nonce,
                requestId,
                response);
            return response;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(responsePayload);
        }
    }

    private static async Task<Stream> ConnectAsync(
        LocalServiceControlEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsWindows())
        {
            var pipe = new NamedPipeClientStream(
                ".",
                endpoint.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            try
            {
                await pipe.ConnectAsync(
                    cancellationToken).ConfigureAwait(false);
                return pipe;
            }
            catch
            {
                pipe.Dispose();
                throw;
            }
        }

        var socket = new Socket(
            AddressFamily.Unix,
            SocketType.Stream,
            ProtocolType.Unspecified);
        try
        {
            await socket.ConnectAsync(
                new UnixDomainSocketEndPoint(
                    endpoint.UnixSocketPath),
                cancellationToken).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
