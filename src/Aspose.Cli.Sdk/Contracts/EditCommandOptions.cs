namespace Aspose.Cli.Sdk.Contracts;

/// <summary>Execution semantics shared by bounded product edit commands.</summary>
public sealed record EditCommandOptions
{
    /// <summary>Optional SHA-256 precondition copied from an input fingerprint.</summary>
    public string? IfMatch { get; init; }

    /// <summary>Apply and validate in memory without publishing an output.</summary>
    public bool DryRun { get; init; }

    /// <summary>Keep successful operations and report failures as a partial result.</summary>
    public bool BestEffort { get; init; }
}
