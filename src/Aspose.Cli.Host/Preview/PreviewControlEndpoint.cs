using System.Text.Json;
using Aspose.Cli.Host.LocalServices;

namespace Aspose.Cli.Host.Preview;

/// <summary>
/// Preview business adapter over the shared current-user, framed
/// local-service control protocol.
/// </summary>
internal sealed class PreviewControlEndpoint : IDisposable
{
    private readonly LocalServiceControlEndpoint _endpoint;
    private readonly Action _requestStop;
    private readonly Func<PreviewInteractiveState> _readStatus;
    private readonly LocalServiceControlServer _server;

    public PreviewControlEndpoint(
        string id,
        string nonce,
        string token,
        Action requestStop,
        Func<PreviewInteractiveState> readStatus)
    {
        _endpoint = Endpoint(id);
        _requestStop = requestStop;
        _readStatus = readStatus;
        _server = new LocalServiceControlServer(
            _endpoint,
            nonce,
            token,
            Dispatch,
            afterResponse: AfterResponse);
    }

    public void Start() => _server.Start();

    public static LocalServiceControlResponse Stop(
        PreviewSessionMarker marker,
        string? tokenOverride = null) =>
        Send(marker, "stop", tokenOverride);

    public static LocalServiceControlResponse Status(
        PreviewSessionMarker marker) =>
        Send(marker, "status", tokenOverride: null);

    public void Dispose() => _server.Dispose();

    private static LocalServiceControlResponse Send(
        PreviewSessionMarker marker,
        string command,
        string? tokenOverride) =>
        LocalServiceControlServer.Send(
            Endpoint(marker.Id),
            marker.Nonce,
            tokenOverride ?? marker.Token,
            command);

    private LocalServiceControlResponse Dispatch(
        LocalServiceControlRequest request)
    {
        if (string.Equals(
            request.Command,
            "stop",
            StringComparison.Ordinal))
        {
            return Response(ok: true);
        }

        if (string.Equals(
            request.Command,
            "status",
            StringComparison.Ordinal))
        {
            return Response(
                ok: true,
                result: _readStatus());
        }

        return Response(
            ok: false,
            message: "Unknown preview control command.");
    }

    private void AfterResponse(LocalServiceControlRequest request)
    {
        if (string.Equals(
            request.Command,
            "stop",
            StringComparison.Ordinal))
        {
            _requestStop();
        }
    }

    private static LocalServiceControlResponse Response(
        bool ok,
        string? message = null,
        PreviewInteractiveState? result = null) => new(
        0,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        ok,
        message,
        Result: result is null
            ? null
            : JsonSerializer.SerializeToElement(
                result,
                PreviewLocalServiceJsonContext.Default.PreviewInteractiveState));

    private static LocalServiceControlEndpoint Endpoint(
        string id) => new("preview", id);
}
