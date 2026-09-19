using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>Result of starting or reusing a product preview session.</summary>
public sealed record ProductPreviewStartResult()
    : ResultEnvelope(CommonSchemaIds.PreviewSession, 2)
{
    /// <summary>Opaque session identifier used by status and stop.</summary>
    [JsonPropertyOrder(-50)]
    public required string Id { get; init; }

    /// <summary>Product that owns the preview.</summary>
    public required string Product { get; init; }

    /// <summary>Loopback browser URL.</summary>
    public required string Url { get; init; }

    /// <summary>Background process identifier.</summary>
    public required int Pid { get; init; }

    /// <summary>Absolute source file path.</summary>
    public required string File { get; init; }

    /// <summary>Effective product preview view.</summary>
    public required string View { get; init; }

    /// <summary>Opaque product-owned selector.</summary>
    public ProductPreviewPayload? Selector { get; init; }

    /// <summary><c>true</c> when an equivalent live session was reused.</summary>
    public required bool Reused { get; init; }
}

/// <summary>Result of listing product preview sessions.</summary>
public sealed record ProductPreviewStatusResult()
    : ResultEnvelope(CommonSchemaIds.PreviewStatus, 2)
{
    /// <summary>Live sessions owned by the current user.</summary>
    public required IReadOnlyList<ProductPreviewSessionInfo> Sessions { get; init; }
}

/// <summary>Result of stopping product preview sessions.</summary>
public sealed record ProductPreviewStopResult()
    : ResultEnvelope(CommonSchemaIds.PreviewStatus, 2)
{
    /// <summary>Session identifiers that accepted an authenticated stop request.</summary>
    public required IReadOnlyList<string> Stopped { get; init; }

    /// <summary>Sessions still live after the request.</summary>
    public required IReadOnlyList<ProductPreviewSessionInfo> Sessions { get; init; }
}

/// <summary>Discoverable state of one current-user product preview.</summary>
public sealed record ProductPreviewSessionInfo
{
    /// <summary>Opaque session identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Product that owns the preview.</summary>
    public required string Product { get; init; }

    /// <summary>Loopback browser URL.</summary>
    public required string Url { get; init; }

    /// <summary>Background process identifier.</summary>
    public required int Pid { get; init; }

    /// <summary>Absolute source file path.</summary>
    public required string File { get; init; }

    /// <summary>Effective product preview view.</summary>
    public required string View { get; init; }

    /// <summary>Opaque product-owned selector.</summary>
    public ProductPreviewPayload? Selector { get; init; }

    /// <summary>Current published document revision, when the live process reported it.</summary>
    public int? Revision { get; init; }
}
