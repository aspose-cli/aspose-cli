using System.Globalization;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Extensibility.Output;

/// <summary>
/// Human-readable lines for the result parts every product shares: input titles, produced
/// files, detail sections, safety backups, bounded-edit outcomes, the pages they touched and
/// their verification. Products add only their own evidence. A value or list that is absent or
/// empty is written as <see cref="TableText.Missing"/>.
/// </summary>
public static class ResultText
{
    /// <summary>Writes the title line of an info result: <c>PATH (FORMAT, N bytes)</c>.</summary>
    public static void Input(TableSurface surface, InputInfo input)
    {
        ArgumentNullException.ThrowIfNull(input);
        surface.Out.WriteLine($"{input.Path} ({input.Format}, {TableText.Bytes(input.SizeBytes)})");
    }

    /// <summary>Writes <c>wrote PATH (FORMAT, N bytes[, detail])</c>.</summary>
    public static void Produced(TableSurface surface, OutputInfo output, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        surface.Out.WriteLine($"wrote {FileFacts(output.Path, output.Format, output.SizeBytes, detail)}");
    }

    /// <summary>
    /// Writes the files a command produced as one count line, <c>VERB N UNIT(s)</c> such as
    /// <c>rendered 3 page(s)</c>, then one <c>  LABEL: PATH (FORMAT, N bytes[, detail])</c> line
    /// per file.
    /// </summary>
    /// <param name="surface">The surface to write to.</param>
    /// <param name="verb">What the command did to the files, such as <c>rendered</c> or <c>extracted</c>.</param>
    /// <param name="unit">What one file holds, such as <c>page</c> or <c>image item</c>.</param>
    /// <param name="files">The files, in result order.</param>
    public static void Files(TableSurface surface, string verb, string unit, IReadOnlyList<ProducedFile> files)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(verb);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentNullException.ThrowIfNull(files);
        surface.Out.WriteLine($"{verb} {TableText.Int(files.Count)} {unit}(s)");
        foreach (ProducedFile file in files)
        {
            string label = file.Label is null ? string.Empty : $"{file.Label}: ";
            surface.Out.WriteLine($"  {label}{FileFacts(file.Path, file.Format, file.SizeBytes, file.Detail)}");
        }
    }

    /// <summary>
    /// Starts a requested detail section: a blank line, then <c>### TITLE</c> in markdown or
    /// <c>TITLE:</c> in plain text, then <see cref="TableText.Missing"/> when the section is empty.
    /// </summary>
    /// <returns>Whether the caller should write the section content.</returns>
    public static bool Section(TableSurface surface, string title, bool empty = false)
    {
        surface.Out.WriteLine();
        if (surface.Format == TableFormat.Markdown)
        {
            surface.Out.WriteLine($"### {title}");
            surface.Out.WriteLine();
        }
        else
        {
            surface.Out.WriteLine($"{title}:");
        }

        if (empty)
        {
            surface.Out.WriteLine(TableText.Missing);
        }

        return !empty;
    }

    /// <summary>
    /// Writes a requested detail list as a table under its <see cref="Section"/> heading, one
    /// row per item; a cell without a value is <see cref="TableText.Missing"/>. A list that was not
    /// requested (<see langword="null"/>) writes nothing; an empty one writes its heading and
    /// <see cref="TableText.Missing"/>.
    /// </summary>
    public static void Table<T>(
        TableSurface surface,
        string title,
        IReadOnlyList<T>? items,
        string[] columns,
        Func<T, string?[]> row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (items is null || !Section(surface, title, items.Count == 0))
        {
            return;
        }

        var table = new TextTable(columns);
        foreach (T item in items)
        {
            table.AddRow(row(item));
        }

        table.WriteTo(surface.Out, surface.Format);
    }

    /// <summary>
    /// Writes a requested list of names on one comma-separated line under its
    /// <see cref="Section"/> heading; <see langword="null"/> writes nothing.
    /// </summary>
    public static void List(TableSurface surface, string title, IReadOnlyList<string>? values)
    {
        if (values is not null && Section(surface, title, values.Count == 0))
        {
            surface.Out.WriteLine(string.Join(", ", values));
        }
    }

    /// <summary>
    /// Writes a requested name/value map, such as document properties or metadata, as a
    /// <c>name</c>/<c>value</c> <see cref="Table{T}"/> ordered by ordinal name; a value the map holds as
    /// <see langword="null"/> is <see cref="TableText.Missing"/>. <see langword="null"/> writes nothing.
    /// </summary>
    public static void Properties(TableSurface surface, string title, IReadOnlyDictionary<string, string?>? properties)
    {
        if (properties is null)
        {
            return;
        }

        KeyValuePair<string, string?>[] rows = [.. properties.OrderBy(static property => property.Key, StringComparer.Ordinal)];
        Table(surface, title, rows, ["name", "value"], static property => [property.Key, property.Value]);
    }

    /// <summary>Writes the backup line when an in-place edit made or kept one.</summary>
    public static void Backup(TableSurface surface, BackupInfo? backup)
    {
        if (backup is not null)
        {
            string state = backup.Created ? "created"
                : backup.HoldsReplacedVersion ? "kept existing"
                : $"kept existing, an earlier version last written {backup.LastWriteUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} UTC";
            surface.Out.WriteLine($"backup: {backup.Path} ({state})");
        }
    }

    /// <summary>
    /// Writes <c>UNITS touched: A, B</c>, such as <c>pages touched: 1, 3</c>, when an edit reports
    /// the parts it changed; nothing when it changed none.
    /// </summary>
    /// <param name="surface">The surface to write to.</param>
    /// <param name="units">The plural unit, such as <c>pages</c> or <c>slide ids</c>.</param>
    /// <param name="touched">The parts the edit changed, or <see langword="null"/>.</param>
    public static void Touched<T>(TableSurface surface, string units, IReadOnlyList<T>? touched)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(units);
        if (touched is { Count: > 0 })
        {
            surface.Out.WriteLine($"{units} touched: {string.Join(", ", touched.Select(static item => Convert.ToString(item, CultureInfo.InvariantCulture)))}");
        }
    }

    /// <summary>
    /// Writes the read-back verification of an edit: <c>verification: ok</c> or
    /// <c>verification: needs attention</c>, the operations it checked and
    /// <paramref name="evidence"/>, then for each issue a <c>  CODE [LOCATION]: MESSAGE</c> line,
    /// without the location when it has none, and a <c>    hint: HINT</c> line when it has a hint.
    /// </summary>
    /// <param name="surface">The surface to write to.</param>
    /// <param name="verification">The verification, which may be absent.</param>
    /// <param name="evidence">Product evidence written after the checked operations, such as <c>2 direct</c>.</param>
    public static void Verification(TableSurface surface, EditVerification? verification, string? evidence = null)
    {
        if (verification is null)
        {
            return;
        }

        string checkedOps = verification.CheckedOps.Count == 0 ? TableText.Missing : string.Join(", ", verification.CheckedOps);
        string facts = evidence is null ? string.Empty : $"; {evidence}";
        surface.Out.WriteLine($"verification: {(verification.Ok ? "ok" : "needs attention")}; checked ops: {checkedOps}{facts}");
        foreach (VerificationIssue issue in verification.Issues)
        {
            string at = issue.Location is null ? string.Empty : $" [{issue.Location}]";
            surface.Out.WriteLine($"  {issue.Code}{at}: {issue.Message}");
            if (issue.Hint is { } hint)
            {
                surface.Out.WriteLine($"    hint: {hint}");
            }
        }
    }

    /// <summary>
    /// Writes a bounded edit: the dry-run or produced-file headline, one line per operation
    /// with the error and hint of every failed one, and the backup.
    /// </summary>
    public static void Edit(
        TableSurface surface,
        bool dryRun,
        OutputInfo? output,
        IReadOnlyList<BoundedOperationOutcome> applied,
        BackupInfo? backup)
    {
        ArgumentNullException.ThrowIfNull(applied);
        int failed = applied.Count(static op => op.Status == OpStatuses.Failed);
        string outcome = string.Create(
            CultureInfo.InvariantCulture,
            $"{applied.Count - failed} of {applied.Count} op(s) applied{(failed == 0 ? string.Empty : $", {failed} failed")}");
        if (dryRun)
        {
            surface.Out.WriteLine($"dry run: {outcome}; nothing was written");
        }
        else if (output is not null)
        {
            Produced(surface, output, outcome);
        }
        else
        {
            surface.Out.WriteLine(outcome);
        }

        foreach (BoundedOperationOutcome op in applied)
        {
            surface.Out.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  [{op.Id}] {op.Op}: {op.Status} ({op.ItemsAffected} item(s))"));
            if (op.Error is { } error)
            {
                surface.Out.WriteLine($"      {error.Code}: {error.Message}");
                if (error.Hint is { } hint)
                {
                    surface.Out.WriteLine($"      hint: {hint}");
                }
            }
        }

        Backup(surface, backup);
    }

    private static string FileFacts(string path, string? format, long sizeBytes, string? detail)
    {
        string facts = string.Join(", ", new[] { format, TableText.Bytes(sizeBytes), detail }.OfType<string>());
        return $"{path} ({facts})";
    }
}

/// <summary>One file in a <see cref="ResultText.Files"/> listing.</summary>
/// <param name="Label">What the file holds, such as <c>page 2</c>; null when the path says it.</param>
/// <param name="Path">The file's path.</param>
/// <param name="Format">The file's format id; null when the result does not state one.</param>
/// <param name="SizeBytes">The file's size.</param>
/// <param name="Detail">Further facts, such as <c>pages 1-3</c>; null when there are none.</param>
public sealed record ProducedFile(string? Label, string Path, string? Format, long SizeBytes, string? Detail = null);
