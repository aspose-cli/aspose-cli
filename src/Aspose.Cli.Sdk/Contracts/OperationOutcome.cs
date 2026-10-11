namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// A command result whose process exit code depends on what it found: the command completed,
/// but when the result does not pass or is incomplete, such as a failed operation or a missing
/// font, it exits 8.
/// </summary>
public interface IPartialOutcome
{
    /// <summary>Whether at least one item failed or did not pass.</summary>
    bool HasFailures { get; }
}

/// <summary>The stable wire result for one bounded edit operation.</summary>
[SchemaId("operation-outcome")]
public sealed record BoundedOperationOutcome
{
    /// <summary>Stable correlation id from the input batch.</summary>
    [Pattern("^[A-Za-z][A-Za-z0-9._-]{0,63}$")]
    public required string Id { get; init; }

    /// <summary>Product-owned operation id.</summary>
    public required string Op { get; init; }

    /// <summary>Whether the operation applied (<c>ok</c>) or <c>failed</c>.</summary>
    [AllowedValues(typeof(OpStatuses))]
    public required string Status { get; init; }

    /// <summary>Number of product-owned items changed by the operation.</summary>
    [Minimum(0)]
    public required long ItemsAffected { get; init; }

    /// <summary>The most targets one outcome lists.</summary>
    public const int MaximumTargets = 100;

    /// <summary>Product-owned stable addresses; failed outcomes identify attempted targets.</summary>
    [MaxItems(MaximumTargets)]
    public IReadOnlyList<string> Targets { get; init; } = [];

    /// <summary>
    /// The error that stopped the operation, as an error envelope states it; present only when
    /// <see cref="Status"/> is <c>failed</c>.
    /// </summary>
    public ErrorPayload? Error { get; init; }
}

/// <summary>Stable wire values used for per-operation status.</summary>
public static class OpStatuses
{
    public const string Ok = "ok";
    public const string Failed = "failed";
}
