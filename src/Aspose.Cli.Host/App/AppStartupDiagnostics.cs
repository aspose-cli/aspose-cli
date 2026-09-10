using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Security;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Host.App;

internal enum AppStartupStage
{
    Configuration,
    Permissions,
    Storage,
    Listener,
    Control,
    BrowserLaunch,
}

/// <summary>Maps expected App startup failures to bounded public diagnostics.</summary>
internal static class AppStartupDiagnostics
{
    public static bool IsExpected(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or SecurityException
            or JsonException
            or HttpListenerException
            or SocketException
            or Win32Exception;

    public static AppStartupStage ConfigurationStage(Exception exception) =>
        IsPermissionFailure(exception)
            ? AppStartupStage.Permissions
            : exception is JsonException
                ? AppStartupStage.Configuration
                : AppStartupStage.Storage;

    public static CliException Failure(
        AppStartupStage stage,
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        string id = Id(stage);
        return new CliException(
            ErrorCodes.AppStartupFailed,
            $"The local App could not complete startup stage '{id}'.",
            hint: Hint(stage),
            details: new JsonObject
            {
                ["stage"] = id,
                ["reason"] = Reason(exception),
            },
            innerException: exception);
    }

    public static string BrowserWarning(
        Exception exception,
        string url) =>
        "app: browser-launch stage failed ("
        + DiagnosticRedactor.Redact(exception.Message)
        + $"); open {url}";

    public static bool TryReadStage(
        string diagnostic,
        out AppStartupStage stage)
    {
        foreach (AppStartupStage candidate in
            Enum.GetValues<AppStartupStage>())
        {
            if (diagnostic.Contains(
                    $"stage '{Id(candidate)}'",
                    StringComparison.Ordinal))
            {
                stage = candidate;
                return true;
            }
        }

        stage = default;
        return false;
    }

    private static string Id(AppStartupStage stage) => stage switch
    {
        AppStartupStage.Configuration => "configuration",
        AppStartupStage.Permissions => "permissions",
        AppStartupStage.Storage => "storage",
        AppStartupStage.Listener => "listener",
        AppStartupStage.Control => "control",
        AppStartupStage.BrowserLaunch => "browser-launch",
        _ => throw new ArgumentOutOfRangeException(nameof(stage)),
    };

    private static string Hint(AppStartupStage stage) => stage switch
    {
        AppStartupStage.Configuration =>
            "Run 'aspose-cli doctor' and correct the reported App configuration.",
        AppStartupStage.Permissions =>
            "Allow the current user to access its Aspose CLI configuration and temporary directories, then retry.",
        AppStartupStage.Storage =>
            "Check available space and the current user's App storage, then retry.",
        AppStartupStage.Listener =>
            "Check loopback HTTP permissions and retry with '--port 0' or another port.",
        AppStartupStage.Control =>
            "Run 'aspose-cli app stop', then retry; check current-user pipe permissions if the failure remains.",
        AppStartupStage.BrowserLaunch =>
            "Open the URL printed by the command in a browser.",
        _ => throw new ArgumentOutOfRangeException(nameof(stage)),
    };

    private static string Reason(Exception exception)
    {
        if (exception is CliException cli
            && cli.Details?["reason"] is JsonValue reason
            && reason.TryGetValue(out string? value)
            && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return exception switch
        {
            _ when IsPermissionFailure(exception) =>
                "permission-denied",
            JsonException => "invalid-configuration",
            HttpListenerException or SocketException =>
                "listener-unavailable",
            IOException => "io-failure",
            Win32Exception => "platform-failure",
            _ => "startup-failure",
        };
    }

    private static bool IsPermissionFailure(Exception exception) =>
        exception is UnauthorizedAccessException or SecurityException
        || exception is HttpListenerException listener
            && listener.ErrorCode is 5 or 13 or 10013
        || exception is SocketException socket
            && socket.SocketErrorCode == SocketError.AccessDenied
        || exception is Win32Exception windows
            && windows.NativeErrorCode is 5 or 13;
}
