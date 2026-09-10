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
public sealed record BoundedOperationOutcome
{
    /// <summary>Stable correlation id from the input batch.</summary>
    public required string Id { get; init; }

    /// <summary>Zero-based position in the input batch.</summary>
    public required int Index { get; init; }

    /// <summary>Product-owned operation id.</summary>
    public required string Op { get; init; }

    /// <summary>One of <see cref="OpStatuses"/>.</summary>
    public required string Status { get; init; }

    /// <summary>Number of product-owned items changed by the operation.</summary>
    public required long ItemsAffected { get; init; }

    /// <summary>Product-owned stable addresses; failed outcomes identify attempted targets.</summary>
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
}

/// <summary>Stable wire values used for per-operation status.</summary>
public static class OpStatuses
{
    public const string Ok = "ok";
    public const string Failed = "failed";
}
