namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// Product-neutral issue details. Product-specific addresses belong beside
/// this value in the owning product contract.
/// </summary>
public sealed record ProductIssue
{
    /// <summary>Stable machine-readable issue code.</summary>
    public required string Code { get; init; }

    /// <summary>One of <see cref="ProductIssueSeverities"/>.</summary>
    public required string Severity { get; init; }

    /// <summary>Human-readable explanation of the issue.</summary>
    public required string Message { get; init; }

    /// <summary>Actionable correction, when one is known.</summary>
    public string? Hint { get; init; }
}

/// <summary>Stable wire values used for product issue severity.</summary>
public static class ProductIssueSeverities
{
    public const string Error = "error";
    public const string Warning = "warning";
    public const string Info = "info";
}
