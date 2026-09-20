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

    /// <summary>Closes every document and ends the service.</summary>
    public const string Stop = "stop";
}

/// <summary>How a document should be opened.</summary>
internal sealed record ViewerOpenRequest
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
    public required ViewerDocumentState Document { get; init; }

    /// <summary>Whether the document was already open the same way.</summary>
    public required bool Reused { get; init; }
}

internal sealed record ViewerStatusResponse
{
    public required int Pid { get; init; }

    public required string Url { get; init; }

    public required IReadOnlyList<ViewerDocumentState> Documents { get; init; }
}

internal sealed record ViewerStopResponse
{
    public required IReadOnlyList<string> Stopped { get; init; }

    public required IReadOnlyList<ViewerDocumentState> Documents { get; init; }
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
[JsonSerializable(typeof(ViewerFailure))]
[JsonSerializable(typeof(ViewerServiceMarker))]
[JsonSerializable(typeof(ViewerServiceSecrets))]
internal sealed partial class ViewerServiceJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
