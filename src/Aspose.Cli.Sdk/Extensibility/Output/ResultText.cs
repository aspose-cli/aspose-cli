using System.Globalization;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Extensibility.Output;

/// <summary>
/// Human-readable lines for the result parts every product shares: produced files,
/// safety backups and bounded-edit outcomes. Products add only their own evidence.
/// </summary>
public static class ResultText
{
    /// <summary>Writes <c>wrote PATH (FORMAT, N bytes[, detail])</c>.</summary>
    public static void Produced(TableSurface surface, OutputInfo output, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        string suffix = detail is null ? string.Empty : $", {detail}";
        surface.Out.WriteLine($"wrote {output.Path} ({output.Format}, {TableText.Bytes(output.SizeBytes)}{suffix})");
    }

    /// <summary>Writes the backup line when an in-place edit made or kept one.</summary>
    public static void Backup(TableSurface surface, BackupInfo? backup)
    {
        if (backup is not null)
        {
            surface.Out.WriteLine($"backup: {backup.Path} ({(backup.Created ? "created" : "kept existing")})");
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
