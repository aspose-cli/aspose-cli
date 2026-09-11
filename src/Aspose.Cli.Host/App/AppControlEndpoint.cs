using System.Text.Json;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Host.App;

internal sealed record AppControlResponse(
    bool Ok,
    string? Message,
    AppResult? Result);

/// <summary>
/// App business adapter over the shared current-user, framed local-service
/// control protocol.
/// </summary>
internal sealed class AppControlEndpoint : IDisposable
{
    private static readonly LocalServiceControlEndpoint Endpoint =
        new("app", "singleton");
    private readonly AppHost _host;
    private readonly LocalServiceControlServer _server;

    public AppControlEndpoint(
        AppHost host,
        string nonce,
        string token)
    {
        _host = host;
        _server = new LocalServiceControlServer(
            Endpoint,
            nonce,
            token,
            Dispatch,
            afterResponse: AfterResponse,
            describeFailure: DescribeFailure);
    }

    public void Start() => _server.Start();

    public void Dispose() => _server.Dispose();

    public static AppControlResponse Send(
        AppInstance marker,
        string command,
        string? path = null)
    {
        LocalServiceControlResponse response =
            LocalServiceControlServer.Send(
            Endpoint,
            marker.Nonce,
            marker.Token,
            command,
            path);
        AppResult? result = response.Result is JsonElement payload
            ? payload.Deserialize(
                AppLocalServiceJsonContext.Default.AppResult)
            : null;
        return new AppControlResponse(
            response.Ok,
            response.Message,
            result);
    }

    private LocalServiceControlResponse Dispatch(
        LocalServiceControlRequest request)
    {
        switch (request.Command)
        {
            case "open" when request.Path is not null:
                _host.Workspace.OpenPath(
                    request.Path,
                    uploadedCopy: false);
                return Response(
                    ok: true,
                    _host.Result(reused: true));
            case "activate":
                return Response(
                    ok: true,
                    _host.Activate(request.Path ?? AppRoutes.Home));
            case "stop":
                return Response(
                    ok: true,
                    _host.Result(reused: true));
            default:
                return Response(
                    ok: false,
                    message: "Unknown local App control command.");
        }
    }

    private static string DescribeFailure(Exception exception)
    {
        ProcessFailureLog.Write("app-control", exception);
        return exception is CliException error ? DiagnosticRedactor.Redact(error.Message)
            : "The App could not complete the request. Check its private diagnostics.";
    }

    private void AfterResponse(LocalServiceControlRequest request)
    {
        if (string.Equals(
            request.Command,
            "stop",
            StringComparison.Ordinal))
        {
            _host.RequestStop();
        }
    }

    private static LocalServiceControlResponse Response(
        bool ok,
        AppResult? result = null,
        string? message = null) => new(
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
                AppLocalServiceJsonContext.Default.AppResult));
}
