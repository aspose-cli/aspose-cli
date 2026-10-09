using System.Text.Json.Serialization;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>Result of starting or reusing a product preview session.</summary>
public sealed record ProductPreviewStartResult()
    : ResultEnvelope("preview-session", 2)
{
    /// <summary>Opaque session identifier used by status and stop.</summary>
    [JsonPropertyOrder(-50)]
    [Pattern("^[0-9a-f]{32}$")]
    public required string Id { get; init; }

    /// <summary>Product that owns the preview.</summary>
    [Pattern("^[a-z0-9][a-z0-9-]*$")]
    public required string Product { get; init; }

    /// <summary>Loopback browser URL of the document.</summary>
    [Pattern("^http://127\\.0\\.0\\.1:[0-9]+/d/[0-9a-f]{32}/$")]
    public required string Url { get; init; }

    /// <summary>Background process identifier.</summary>
    [Minimum(1)]
    public required int Pid { get; init; }

    /// <summary>Absolute source file path.</summary>
    public required string File { get; init; }

    /// <summary>Effective product preview view.</summary>
    [MinLength(1)]
    public required string View { get; init; }

    /// <summary><c>true</c> when an equivalent live session was reused.</summary>
    public required bool Reused { get; init; }
}

/// <summary>
/// Result of <c>aspose-cli preview status</c>, which lists the live product preview sessions, or
/// of <c>preview stop</c>, which also names the sessions it stopped.
/// </summary>
public sealed record ProductPreviewStatusResult()
    : ResultEnvelope("preview-status", 2)
{
    /// <summary>
    /// Session identifiers that accepted an authenticated stop request; present only in the
    /// result of <c>preview stop</c>.
    /// </summary>
    [Pattern("^[0-9a-f]{32}$")]
    public IReadOnlyList<string>? Stopped { get; init; }

    /// <summary>Live sessions owned by the current user; after <c>preview stop</c>, the sessions still live.</summary>
    public required IReadOnlyList<ProductPreviewSessionInfo> Sessions { get; init; }
}

/// <summary>Discoverable state of one current-user product preview.</summary>
public sealed record ProductPreviewSessionInfo
{
    /// <summary>Opaque session identifier.</summary>
    [Pattern("^[0-9a-f]{32}$")]
    public required string Id { get; init; }

    /// <summary>Product that owns the preview.</summary>
    [Pattern("^[a-z0-9][a-z0-9-]*$")]
    public required string Product { get; init; }

    /// <summary>Loopback browser URL of the document.</summary>
    [Pattern("^http://127\\.0\\.0\\.1:[0-9]+/d/[0-9a-f]{32}/$")]
    public required string Url { get; init; }

    /// <summary>Background process identifier.</summary>
    [Minimum(1)]
    public required int Pid { get; init; }

    /// <summary>Absolute source file path.</summary>
    public required string File { get; init; }

    /// <summary>Effective product preview view.</summary>
    [MinLength(1)]
    public required string View { get; init; }

    /// <summary>Current published document revision, when the live process reported it.</summary>
    [Minimum(1)]
    public int? Revision { get; init; }
}
