using System.Text.Json.Serialization;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// Result of <c>aspose-cli app</c>: the local browser application was started,
/// reused, queried or stopped.
/// </summary>
public sealed record AppResult() : ResultEnvelope(CommonSchemaIds.App, 2)
{
    /// <summary>Whether a local application instance is running after the command.</summary>
    [JsonPropertyOrder(-50)]
    public required bool Running { get; init; }

    /// <summary>Loopback URL of the application, omitted when it is not running.</summary>
    [JsonPropertyOrder(-49)]
    public string? Url { get; init; }

    /// <summary>Resolved loopback port, omitted when it is not running.</summary>
    public int? Port { get; init; }

    /// <summary>Process id of the application host, omitted when it is not running.</summary>
    public int? Pid { get; init; }

    /// <summary>True when the command activated an existing application instance.</summary>
    public required bool Reused { get; init; }

    /// <summary>Initial application route: <c>welcome</c>, <c>home</c>, <c>preview</c> or <c>settings</c>.</summary>
    public required string Route { get; init; }

    /// <summary>Current document file name, when one is open. Full paths are never exposed.</summary>
    public string? File { get; init; }
}

/// <summary>Stable route identifiers emitted by <see cref="AppResult"/>.</summary>
public static class AppRoutes
{
    /// <summary>First-run license and evaluation choice.</summary>
    public const string Welcome = "welcome";

    /// <summary>Document picker and recent files.</summary>
    public const string Home = "home";

    /// <summary>Live document preview.</summary>
    public const string Preview = "preview";

    /// <summary>License, preferences and diagnostics.</summary>
    public const string Settings = "settings";
}
