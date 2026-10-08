using System.IO.Pipes;
using System.Net.Sockets;
using System.Security.Cryptography;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;

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
        string? payload = null,
        OperationDeadline? operationDeadline = null)
    {
        TimeSpan budget = timeout ?? LocalServiceControlCodec.DefaultStageTimeout;
        using var ownedDeadline = operationDeadline is null ? OperationDeadline.Start(budget) : null;
        OperationDeadline deadline = operationDeadline ?? ownedDeadline!;
        try
        {
            deadline.ThrowIfExpired("control-connect");
            using var frame = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            frame.CancelAfter(LocalServiceControlCodec.DefaultStageTimeout);
            Stream connected;
            // Retry connection only: resending a request after dispatch could repeat a mutation.
            for (int attempt = 0; ; attempt++)
            {
                try { connected = ConnectAsync(endpoint, frame.Token).GetAwaiter().GetResult(); break; }
                catch (Exception exception) when (attempt == 0 && exception is IOException or SocketException)
                { if (deadline.Token.WaitHandle.WaitOne(25)) { deadline.ThrowIfExpired("control-connect"); } }
            }
            using Stream stream = connected;
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
                payload,
                deadline.ExpiresAtTick);
            byte[] requestPayload = LocalServiceControlCodec.Serialize(request);
            try
            {
                LocalServiceControlCodec.WriteFrameAsync(
                    stream,
                    requestPayload,
                    frame.Token).GetAwaiter().GetResult();
            }
            finally
            {
                CryptographicOperations.ZeroMemory(requestPayload);
            }

            // The operation owns its deadline. Allow bounded termination and its final reply.
            using var responseCancellation = new CancellationTokenSource(
                (deadline.Remaining ?? budget) + LocalServiceControlCodec.DefaultStageTimeout);
            using var interrupted = deadline.Token.Register(() =>
            {
                // Expiration allows the server to finish cancellation; an explicit caller cancellation closes the connection.
                if (!deadline.IsExpired) { responseCancellation.Cancel(); }
            });
            byte[] responsePayload = LocalServiceControlCodec.ReadFrameAsync(
                stream, responseCancellation.Token).GetAwaiter().GetResult();
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
        catch (OperationCanceledException)
        {
            deadline.ThrowIfExpired("control-exchange");
            throw CliErrors.OperationTimeout((int)LocalServiceControlCodec.DefaultStageTimeout.TotalSeconds, "control-frame");
        }
    }

    private static async Task<Stream> ConnectAsync(
        LocalServiceControlEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsWindows())
        {
            // CurrentUserOnly makes the client verify the server's owner before it sends
            // the control token and any document password.
            var pipe = new NamedPipeClientStream(
                ".",
                endpoint.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
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
