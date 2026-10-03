using System.Text.Json.Serialization;

namespace Aspose.Cli.Host.ViewerService;

/// <summary>
/// The control channel of the viewer service: what a command can ask the
/// running service for, and what it answers. Requests travel as bounded JSON
/// frames over a pipe only the current user can open.
/// </summary>
internal static class ViewerServiceCommands
{
    /// <summary>Service name of the control endpoint and its start lock.</summary>
    public const string Service = "viewer";

    /// <summary>Hidden command that hosts the service.</summary>
    public const string CommandName = "__viewer-service";

    /// <summary>
    /// Key of a service-wide lock. It carries the configuration directory, so
    /// one user running two isolated configurations gets two services rather
    /// than one blocking the other.
    /// </summary>
    public static string LockKey(string purpose) =>
        purpose + "|" + Aspose.Cli.Sdk.Configuration.ConfigurationPaths.UserDirectory();

    /// <summary>Opens a document, or returns the one already open for it.</summary>
    public const string Open = "open";

    /// <summary>Lists the service and the documents it has open.</summary>
    public const string Status = "status";

    /// <summary>Closes one document.</summary>
    public const string Close = "close";

    /// <summary>Renders open documents again, for example after a license change.</summary>
    public const string Refresh = "refresh";

    /// <summary>Closes every document and ends the service.</summary>
    public const string Stop = "stop";

    /// <summary>Points the App at a page, and opens a file there.</summary>
    public const string App = "app";
}

/// <summary>How a document should be opened.</summary>
internal sealed record ViewerOpenRequest(long MaxInputBytes = Aspose.Cli.Sdk.IO.ResourceBudgetDefaults.DefaultInputBytes)
{
    public required string File { get; init; }

    /// <summary>Product id, or null to select the product from the document.</summary>
    public string? Product { get; init; }

    /// <summary>View id, or null for the product's live view.</summary>
    public string? View { get; init; }

    /// <summary>Presentation effect the viewer plays, for example <c>demo</c>.</summary>
    public string? Effect { get; init; }

    /// <summary>Password of an encrypted document.</summary>
    public string? Password { get; init; }

    /// <summary>Explicit license file, or null for the configured sources.</summary>
    public string? License { get; init; }

    /// <summary>True for <c>--license-mode evaluation</c>: no license source is read.</summary>
    public bool EvaluationRequested { get; init; }

    /// <summary>Explicit font directories, or null for the ambient environment.</summary>
    public IReadOnlyList<string>? FontDirectories { get; init; }
}

/// <summary>One document the service has open.</summary>
internal sealed record ViewerDocumentState
{
    public required string Id { get; init; }

    public required string File { get; init; }

    public required string Product { get; init; }

    public required string View { get; init; }

    public required string Url { get; init; }

    public required int Revision { get; init; }

    /// <summary>License mode the last render ran under.</summary>
    public required string License { get; init; }
}

internal sealed record ViewerOpenResponse
{
    public required int Pid { get; init; }

    public required ViewerDocumentState Document { get; init; }

    /// <summary>Whether the document was already open the same way.</summary>
    public required bool Reused { get; init; }
}

internal sealed record ViewerStatusResponse
{
    public required int Pid { get; init; }

    public required string Url { get; init; }

    public required IReadOnlyList<ViewerDocumentState> Documents { get; init; }

    /// <summary>The page the App is showing, when one is mounted.</summary>
    public string? AppRoute { get; init; }

    /// <summary>File name of the document the App is showing, never its path.</summary>
    public string? AppFile { get; init; }
}

internal sealed record ViewerStopResponse
{
    public required int Pid { get; init; }

    public required IReadOnlyList<string> Stopped { get; init; }

    public required IReadOnlyList<ViewerDocumentState> Documents { get; init; }
}

/// <summary>What the App should show when the browser opens.</summary>
internal sealed record ViewerAppRequest
{
    /// <summary>Page the App opens on: welcome, home, preview or settings.</summary>
    public required string Route { get; init; }

    /// <summary>Absolute path of a file to open, or null to leave the App as it is.</summary>
    public string? File { get; init; }
}

/// <summary>Where the App is, and what it is showing.</summary>
internal sealed record ViewerAppResponse
{
    public required string Url { get; init; }

    public required int Port { get; init; }

    public required int Pid { get; init; }

    public required string Route { get; init; }

    /// <summary>File name of the open document, never its path.</summary>
    public string? File { get; init; }
}

/// <summary>Why the service refused a request, as the command would report it.</summary>
internal sealed record ViewerFailure(string Code, int Exit, string Message);

/// <summary>Where the running viewer service can be found.</summary>
internal sealed record ViewerServiceMarker(
    string Id,
    [property: JsonIgnore] string Token,
    int Pid,
    long StartTicksUtc,
    int Port,
    string Url,
    string Nonce,
    int Version = 1);

/// <summary>The secret companion of the marker; never in the public file.</summary>
internal sealed record ViewerServiceSecrets(string Token);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ViewerOpenRequest))]
[JsonSerializable(typeof(ViewerOpenResponse))]
[JsonSerializable(typeof(ViewerStatusResponse))]
[JsonSerializable(typeof(ViewerStopResponse))]
[JsonSerializable(typeof(ViewerAppRequest))]
[JsonSerializable(typeof(ViewerAppResponse))]
[JsonSerializable(typeof(ViewerFailure))]
[JsonSerializable(typeof(ViewerServiceMarker))]
[JsonSerializable(typeof(ViewerServiceSecrets))]
internal sealed partial class ViewerServiceJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
