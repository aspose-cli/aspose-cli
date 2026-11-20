namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// One command that applies a bounded operation document, and the operations it accepts.
/// Every such command is atomic and supports --dry-run.
/// </summary>
public sealed record ProductOperationDescriptor
{
    /// <summary>Product-relative command path, such as <c>edit</c>.</summary>
    [Pattern("^[a-z][a-z0-9_-]*( [a-z][a-z0-9_-]*)*$")]
    public required string Command { get; init; }

    /// <summary>Product-owned schema id printable through <c>schema &lt;id&gt;</c>.</summary>
    [Pattern("^v2/[a-z][a-z0-9_-]*/[a-z][a-z0-9_/-]*$")]
    public required string InputSchema { get; init; }

    /// <summary>The command that prints one operation's schema, with <c>&lt;op&gt;</c> standing for its name.</summary>
    [Pattern(" schema v2/[a-z][a-z0-9_-]*/[a-z][a-z0-9_/-]*/<op>$")]
    public required string OperationSchema { get; init; }

    /// <summary>
    /// <c>sha256:</c> and the lowercase hex SHA-256 of the served input schema (UTF-8, <c>\n</c>
    /// line endings); it changes whenever the published schema text changes, descriptions
    /// included. It is computed once, when first read.
    /// </summary>
    [Pattern("^sha256:[0-9a-f]{64}$")]
    public string ContractFingerprint => Fingerprint.Value;

    /// <summary>Largest accepted number of operations in one document.</summary>
    [Minimum(1)]
    public required int MaximumOperationCount { get; init; }

    /// <summary>Operation names in published order.</summary>
    [UniqueItems]
    [MinItems(1)]
    [Pattern("^[a-z][a-z0-9_-]*$")]
    public required IReadOnlyList<string> Ops { get; init; }

    /// <summary>The fingerprint of the vocabulary's schema, computed once and shared with the schema.</summary>
    internal Lazy<string> Fingerprint { get; init; } = null!;
}
