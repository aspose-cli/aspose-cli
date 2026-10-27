namespace Aspose.Cli.Sdk.Contracts;

/// <summary>What a dry run checked: the values of <see cref="DryRunResult.Checked"/> and of an edit's <c>checked</c>.</summary>
public static class DryRunChecks
{
    /// <summary>The input files exist and are files the command reads.</summary>
    public const string Input = "input";

    /// <summary>The output resolves to a path the command may write and, for a file, to a format it writes.</summary>
    public const string Output = "output";

    /// <summary>The operation document parses and every operation is valid.</summary>
    public const string Ops = "ops";

    /// <summary>The operations were applied to the document in memory and found their targets.</summary>
    public const string Targets = "targets";
}

/// <summary>
/// Result of <c>--dry-run</c> on a command that writes files, other than an edit, which applies
/// its operations in memory and reports them in its own result: the command line and the files
/// it names were checked, and nothing ran or was written. A dry run that succeeds does not
/// guarantee that the command succeeds.
/// </summary>
public sealed record DryRunResult() : ResultEnvelope("dry-run-result")
{
    /// <summary>Always <c>true</c>: the command ran nothing and wrote no file.</summary>
    [AllowedValues(true)]
    public required bool DryRun { get; init; }

    /// <summary>What the dry run checked, in a fixed order.</summary>
    [MinItems(1), UniqueItems, AllowedValues(typeof(DryRunChecks))]
    public required IReadOnlyList<string> Checked { get; init; }

    /// <summary>
    /// The absolute paths the command would write, replace or delete: an output file, beside
    /// which a command that writes several parts numbers them, or a directory it writes into.
    /// Omitted when the command would write nothing, such as a compare without an output.
    /// </summary>
    [MinItems(1)]
    public IReadOnlyList<string>? Paths { get; init; }

    /// <summary>The format id the command would write its output file in; omitted for a directory.</summary>
    [MinLength(1)]
    public string? Format { get; init; }

    /// <summary>A dry run that checked <paramref name="checks"/> and would write <paramref name="paths"/>.</summary>
    /// <param name="checks">What was checked, among <see cref="DryRunChecks"/>.</param>
    /// <param name="paths">The absolute paths the command would write; none when it would write nothing.</param>
    /// <param name="format">The format of the output file, or null.</param>
    public static DryRunResult Of(IEnumerable<string> checks, IEnumerable<string> paths, string? format = null)
    {
        ArgumentNullException.ThrowIfNull(checks);
        ArgumentNullException.ThrowIfNull(paths);
        string[] order = [DryRunChecks.Input, DryRunChecks.Output, DryRunChecks.Ops, DryRunChecks.Targets];
        string[] checkedItems = [.. checks.Distinct(StringComparer.Ordinal).OrderBy(check => Array.IndexOf(order, check))];
        if (checkedItems.Length == 0 || checkedItems.Any(check => !order.Contains(check, StringComparer.Ordinal)))
        {
            throw new ArgumentException("A dry run checks at least one known item.", nameof(checks));
        }

        string[] written = [.. paths.Select(static path => System.IO.Path.GetFullPath(path)).Distinct(FilePaths.Comparer)];
        return new DryRunResult
        {
            DryRun = true,
            Checked = checkedItems,
            Paths = written.Length == 0 ? null : written,
            Format = format,
        };
    }
}
