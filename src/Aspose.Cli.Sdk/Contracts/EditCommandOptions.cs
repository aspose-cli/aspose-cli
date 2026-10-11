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

    /// <summary>
    /// What an edit's dry run checked, which its result reports as <c>checked</c>: the input, the
    /// output, the operations and, applying them in memory, their targets; null when the edit is
    /// no dry run.
    /// </summary>
    public IReadOnlyList<string>? Checked => DryRun
        ? [DryRunChecks.Input, DryRunChecks.Output, DryRunChecks.Ops, DryRunChecks.Targets]
        : null;
}
