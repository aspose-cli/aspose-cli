using System.Globalization;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Extensibility.Output;

/// <summary>
/// Human-readable lines for the result parts every product shares: source titles, produced
/// files, detail sections, safety backups and bounded-edit outcomes. Products add only their own
/// evidence.
/// </summary>
public static class ResultText
{
    /// <summary>Writes the title line of an info result: <c>PATH (FORMAT, N bytes)</c>.</summary>
    public static void Source(TableSurface surface, SourceInfo source)
    {
        ArgumentNullException.ThrowIfNull(source);
        surface.Out.WriteLine($"{source.Path} ({source.Format}, {TableText.Bytes(source.SizeBytes)})");
    }

    /// <summary>
    /// Writes <c>wrote PATH (FORMAT, N bytes[, detail])</c>; a companion file without a format
    /// reads <c>(companion, N bytes)</c>.
    /// </summary>
    public static void Produced(TableSurface surface, OutputInfo output, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        string suffix = detail is null ? string.Empty : $", {detail}";
        surface.Out.WriteLine($"wrote {output.Path} ({output.Format ?? "companion"}, {TableText.Bytes(output.SizeBytes)}{suffix})");
    }

    /// <summary>
    /// Starts a requested detail section: a blank line, then <c>### TITLE</c> in markdown or
    /// <c>TITLE:</c> in plain text, then <c>none</c> when the section is empty.
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
            surface.Out.WriteLine("none");
        }

        return !empty;
    }

    /// <summary>
    /// Writes a requested detail list as a table under its <see cref="Section"/> heading, one
    /// row per item. A list that was not requested (<see langword="null"/>) writes nothing; an
    /// empty one writes its heading and <c>none</c>.
    /// </summary>
    public static void Table<T>(
        TableSurface surface,
        string title,
        IReadOnlyList<T>? items,
        string[] columns,
        Func<T, string[]> row)
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
    /// two-column <see cref="Table{T}"/>.
    /// </summary>
    /// <param name="surface">The surface to write to.</param>
    /// <param name="title">The section heading.</param>
    /// <param name="properties">The map; <see langword="null"/> when it was not requested.</param>
    /// <param name="nameColumn">The heading of the name column.</param>
    /// <param name="missing">The text that stands for a value the map holds as <see langword="null"/>.</param>
    /// <param name="sortByName">Whether to order the rows by ordinal name rather than keep the map's order.</param>
    public static void Properties(
        TableSurface surface,
        string title,
        IReadOnlyDictionary<string, string?>? properties,
        string nameColumn = "name",
        string missing = "",
        bool sortByName = false)
    {
        if (properties is null)
        {
            return;
        }

        KeyValuePair<string, string?>[] rows = sortByName
            ? [.. properties.OrderBy(static property => property.Key, StringComparer.Ordinal)]
            : [.. properties];
        Table(surface, title, rows, [nameColumn, "value"], property => [property.Key, property.Value ?? missing]);
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
                $"  [{op.Id}/{op.Index}] {op.Op}: {op.Status} ({op.ItemsAffected} item(s))"));
            if (op.Error is { } error)
            {
                surface.Out.WriteLine($"      {error.Code}: {error.Message}");
                surface.Out.WriteLine($"      hint: {error.Hint}");
            }
        }

        Backup(surface, backup);
    }
}
