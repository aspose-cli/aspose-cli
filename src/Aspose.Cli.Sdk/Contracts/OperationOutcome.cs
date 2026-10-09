using System.Text.Json.Nodes;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// A command result whose process exit code depends on per-item failures.
/// </summary>
public interface IPartialOutcome
{
    /// <summary>Whether at least one item failed.</summary>
    bool HasFailures { get; }
}

/// <summary>The stable wire result for one bounded edit operation.</summary>
[SchemaId("operation-outcome")]
public sealed record BoundedOperationOutcome
{
    /// <summary>Stable correlation id from the input batch.</summary>
    [Pattern("^[A-Za-z][A-Za-z0-9._-]{0,63}$")]
    public required string Id { get; init; }

    /// <summary>Zero-based position in the input batch.</summary>
    [Minimum(0)]
    public required int Index { get; init; }

    /// <summary>Product-owned operation id.</summary>
    public required string Op { get; init; }

    /// <summary>One of <see cref="OpStatuses"/>.</summary>
    [AllowedValues(typeof(OpStatuses))]
    public required string Status { get; init; }

    /// <summary>Number of product-owned items changed by the operation.</summary>
    [Minimum(0)]
    public required long ItemsAffected { get; init; }

    /// <summary>Product-owned stable addresses; failed outcomes identify attempted targets.</summary>
    [MaxItems(100)]
    public IReadOnlyList<string> Targets { get; init; } = [];

    /// <summary>Failure details; present only when <see cref="Status"/> is <c>failed</c>.</summary>
    public OpError? Error { get; init; }
}

/// <summary>An operation failure reported by a best-effort batch.</summary>
public sealed record OpError
{
    /// <summary>Stable machine-readable error code.</summary>
    public required string Code { get; init; }

    /// <summary>Human-readable failure description.</summary>
    public required string Message { get; init; }

    /// <summary>Actionable recovery guidance for the failed operation.</summary>
    public required string Hint { get; init; }

    /// <summary>
    /// Structured context from the error, such as the names a not-found target could have
    /// been; the same object a failing batch would report in its error envelope.
    /// </summary>
    public JsonObject? Details { get; init; }
}

/// <summary>Stable wire values used for per-operation status.</summary>
public static class OpStatuses
{
    public const string Ok = "ok";
    public const string Failed = "failed";
}
