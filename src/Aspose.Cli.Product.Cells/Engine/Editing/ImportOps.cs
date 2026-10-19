using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.Operations;
using Aspose.Cli.Sdk.Results;
using CellsRange = Aspose.Cells.Range;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>
/// The source workbooks of an edit's import operations. Each distinct file opens once, on first
/// use, through the bounded loader (input budgets, no network resources, password and text-import
/// rules) and is disposed with the batch. Sources are only read; the edited file itself is a
/// separate read of the file as it is on disk.
/// </summary>
internal sealed class CellsImportSources(
    CellsWorkbookLoader loader,
    ResourceBudgetLedger budgets,
    IReadOnlyDictionary<string, Secret>? secrets) : IDisposable
{
    private readonly Dictionary<string, LoadedWorkbook> _opened = new(FilePaths.Comparer);
    private readonly List<Warning> _imports = [];

    internal ResourceBudgetLedger Budgets => budgets;

    /// <summary>
    /// The warnings of the opened sources, such as resources they could not load, and of the
    /// imports that read them.
    /// </summary>
    internal IReadOnlyList<Warning>? Warnings() =>
        EnvelopeParts.CombineWarnings([.. _opened.Values.Select(static loaded => loaded.Warnings()), _imports]);

    internal void Warn(Warning warning) => _imports.Add(warning);

    internal Workbook Open(string path, string? sourcePasswordEnv)
    {
        if (!_opened.TryGetValue(path, out LoadedWorkbook? loaded))
        {
            loaded = loader.Open(path, PasswordSource.OperationField(
                OperationSecrets.Resolve(secrets, sourcePasswordEnv), "sourcePasswordEnv"));

            _opened.Add(path, loaded);
        }

        return loaded.Workbook;
    }

    public void Dispose()
    {
        foreach (LoadedWorkbook loaded in _opened.Values)
        {
            loaded.Dispose();
        }

        _opened.Clear();
    }
}

/// <summary>Imports a range or a whole sheet from another workbook file.</summary>
internal static class ImportOps
{
    public static void ImportRange(Worksheet sheet, ImportRangeOp op, CellsImportSources sources)
    {
        Workbook source = sources.Open(op.Path, op.SourcePasswordEnv);
        RangeSpec spec = A1.ParseRange(op.From);
        Worksheet fromSheet = spec.SheetName is { } sheetName ? Sheets.Resolve(source, sheetName) : source.Worksheets[0];
        RangeRef from = spec.Range;
        (Worksheet toSheet, RangeRef to) = Sheets.ResolveRange(sheet, op.To);

        CellsRange origin = fromSheet.Cells.CreateRange(
            from.Start.Row, from.Start.Column, from.RowCount, from.ColumnCount);
        CellsRange destination = toSheet.Cells.CreateRange(
            to.Start.Row, to.Start.Column, from.RowCount, from.ColumnCount);
        bool everything = op.Content == CellsImportContents.Everything;
        using (new SourceLinks(toSheet.Workbook, source, new CopiedSheet(fromSheet, toSheet)))
        {
            destination.Copy(origin, new PasteOptions
            {
                PasteType = everything ? PasteType.All : PasteType.ValuesAndNumberFormats,
            });
        }

        if (everything)
        {
            WarnUncachedLinks(source, origin, toSheet, to.Start.Row - from.Start.Row, to.Start.Column - from.Start.Column, sources);
        }
    }

    public static void ImportSheet(Workbook workbook, ImportSheetOp op, CellsImportSources sources)
    {
        Workbook source = sources.Open(op.Path, op.SourcePasswordEnv);
        Worksheet origin = op.SourceSheet is { } sheetName ? Sheets.Resolve(source, sheetName) : source.Worksheets[0];
        string name = op.Name ?? origin.Name;
        SheetOps.RequireUnusedName(
            workbook, name, "Give the imported sheet another name with \"name\", or rename or delete the existing sheet first.");

        RefuseConflictingNames(workbook, origin);
        // The source was checked once when it opened; each import adds its sheet again.
        sources.Budgets.Consume(CellsBudgetDomains.Cells, origin.Cells.Count, "items", "edit");
        sources.Budgets.Consume(CellsBudgetDomains.Sheets, 1, "items", "edit");
        sources.Budgets.Consume(CellsBudgetDomains.Objects, origin.Shapes.Count, "items", "edit");

        Worksheet sheet = workbook.Worksheets[workbook.Worksheets.Add()];
        try
        {
            sheet.Name = name;
            using (new SourceLinks(workbook, source))
            {
                sheet.Copy(origin, new CopyOptions());
            }

            WarnUncachedLinks(source, origin.Cells, sheet, 0, 0, sources);
            if (op.At is { } at)
            {
                sheet.MoveTo(at - 1);
            }
        }
        catch
        {
            // A failed import leaves no partial sheet behind, so a best-effort batch goes on
            // with the workbook as it was.
            workbook.Worksheets.RemoveAt(sheet.Index);
            throw;
        }

        // The one sheet it adds.
    }

    /// <summary>
    /// Warns about copied formulas that read another workbook through a link without cached
    /// values. In the source they evaluate to an error such as #REF!, but the copy gives the link
    /// an empty cache, so here they evaluate as if the linked cells were empty, usually to 0
    /// (known issue CELLS-COPY-EXTERNAL-CACHE, KNOWN-ISSUES.md). Only the copied cells of a
    /// source with links are scanned, and only their formulas that show an error and read a link
    /// are evaluated, without calculating other cells or writing a result back, so the edit's
    /// own recalculation, or --no-recalc, decides what the output stores.
    /// </summary>
    private static void WarnUncachedLinks(
        Workbook source,
        System.Collections.IEnumerable copiedFrom,
        Worksheet to,
        int rowOffset,
        int columnOffset,
        CellsImportSources sources)
    {
        if (source.Worksheets.ExternalLinks.Count == 0)
        {
            return;
        }

        var evaluation = new CalculationOptions { Recursive = false };
        var changed = new List<string>();
        var files = new SortedSet<string>(FilePaths.Comparer);
        long scanned = 0;
        foreach (Cell cell in copiedFrom)
        {
            if (++scanned % 4096 == 0)
            {
                sources.Budgets.Deadline.ThrowIfExpired("import-links");
            }

            if (!cell.IsFormula || cell.Type != CellValueType.IsError)
            {
                continue;
            }

            if (!LinkedFiles(cell).Any()
                || to.Cells.CheckCell(cell.Row + rowOffset, cell.Column + columnOffset) is not { IsFormula: true } copied)
            {
                continue;
            }

            // A link back to the edited workbook is internal once copied (CELLS-COPY-SELF-LINK).
            string[] linked = [.. LinkedFiles(copied)];
            if (linked.Length == 0)
            {
                continue;
            }

            // Evaluates the copied formula on its sheet; Recursive = false reads other cells'
            // stored values instead of calculating them, so no cell changes.
            if (to.CalculateFormula("=ISERROR(" + copied.Formula[1..] + ")", evaluation) is false)
            {
                changed.Add(copied.Name);
                files.UnionWith(linked);
            }
        }
        if (changed.Count == 0)
        {
            return;
        }

        const int Listed = 10;
        string sheet = Sheets.QuotedName(to.Name);
        string cells = string.Join(", ", changed.Take(Listed)) + (changed.Count > Listed ? $" and {changed.Count - Listed} more" : string.Empty);
        sources.Warn(new Warning(CellsDiagnostics.ExternalLinkCacheMissing, $"{changed.Count} imported formula(s) on '{to.Name}' ({cells}) read {string.Join(", ", files)} through a link "
                + "that caches no values: in the source they show an error such as #REF!, but here they read the linked cells as empty, "
                + "usually as 0, and so do the formulas that depend on them.")
        {
            // --verify reports it as a verification issue: the output's results differ from the source's.
            AffectsCompleteness = true,
            Hint = "Recalculation never opens the linked workbook. Replace these formulas with set_formula or set_values, or open the "
                + "source in Excel with the linked workbook available and save it so the link caches its values.",
            Docs = "cells/editing",
            Location = sheet + "!" + changed[0],
        });
    }

    private static IEnumerable<string> LinkedFiles(Cell cell) =>
        (cell.GetPrecedents()?.Cast<ReferredArea>() ?? [])
            .Where(static precedent => precedent.IsExternalLink)
            .Select(static precedent => Path.GetFileName(precedent.ExternalFileName));

    /// <summary>
    /// Refuses a source that defines a workbook-level name this workbook defines differently.
    /// Worksheet.Copy then adds the source's definition scoped to this workbook's first sheet,
    /// which changes the results of that sheet's formulas once saved (known issue
    /// CELLS-COPY-NAME-SCOPE, KNOWN-ISSUES.md). A name that refers only to the copied sheet is
    /// scoped to the new sheet correctly.
    /// </summary>
    private static void RefuseConflictingNames(Workbook workbook, Worksheet origin)
    {
        Dictionary<string, Name> defined = WorkbookNames(workbook)
            .GroupBy(static name => name.Text, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);
        string[] conflicts = WorkbookNames(origin.Workbook)
            .Where(name => defined.TryGetValue(name.Text, out Name? existing)
                && !string.Equals(existing.RefersTo, name.RefersTo, StringComparison.OrdinalIgnoreCase)
                && !RefersOnlyTo(name, origin))
            .Select(static name => name.Text)
            .ToArray();
        if (conflicts.Length > 0)
        {
            throw new OperationInvalidException(
                $"the source and this workbook both define the name{(conflicts.Length == 1 ? string.Empty : "s")} {string.Join(", ", conflicts.Select(static text => $"'{text}'"))} differently",
                "Rename or delete the name in one of the workbooks (delete_name, define_name), or import the cells with import_range.");
        }
    }

    // Visible, workbook-scoped names; hidden names are Excel's own.
    private static IEnumerable<Name> WorkbookNames(Workbook workbook) =>
        DefinedNames.Of(workbook).Where(static name => name.SheetIndex == 0 && name.IsVisible);

    private static bool RefersOnlyTo(Name name, Worksheet sheet) =>
        name.GetReferredAreas(true) is { Length: > 0 } areas
        && areas.All(area => !area.IsExternalLink
            && string.Equals(area.SheetName, sheet.Name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The source sheet a range is copied from and the sheet of this workbook it is copied to. A
    /// copied reference that names the source sheet itself shifts with the copy, as one that names
    /// no sheet does, so it means the destination sheet rather than a sheet of that name here.
    /// </summary>
    private sealed record CopiedSheet(Worksheet From, Worksheet To)
    {
        /// <summary>
        /// Gives the destination the source sheet's name while the copy's links are made local, so
        /// the references that name the source sheet land on it, then restores the names; the
        /// references follow the sheets, not the names. A sheet of this workbook with the source
        /// sheet's name steps aside meanwhile. Nothing is renamed when the names already match, or
        /// when the source has another sheet with the destination's name, whose references point
        /// at the destination by that name.
        /// </summary>
        internal IDisposable? NameDestinationAsSource()
        {
            string original = To.Name;
            string name = From.Name;
            if (string.Equals(original, name, StringComparison.OrdinalIgnoreCase)
                || From.Workbook.Worksheets[original] is not null)
            {
                return null;
            }

            // The lookup ignores case, as sheet names do.
            Worksheet? aside = To.Workbook.Worksheets[name];
            string? asideName = aside?.Name;
            aside?.Name = "~" + Guid.NewGuid().ToString("N")[..24];
            To.Name = name;
            return new Restore(() =>
            {
                To.Name = original;
                aside?.Name = asideName!;
            });
        }

        private sealed class Restore(Action restore) : IDisposable
        {
            public void Dispose() => restore();
        }
    }

    /// <summary>
    /// Copying from another workbook turns a reference to a source sheet into a link to the
    /// source file. Disposing removes the links the copy added and makes their references local:
    /// they point at the sheet of the same name in this workbook, or become #REF!. A link the copy
    /// added back to the edited workbook itself is made local too: the copy keeps it as a link
    /// that reads nothing, while a formula written with the same reference becomes an internal
    /// one (known issue CELLS-COPY-SELF-LINK, KNOWN-ISSUES.md).
    /// </summary>
    private readonly struct SourceLinks : IDisposable
    {
        private readonly ExternalLinkCollection _links;
        private readonly int _existing;
        private readonly string _source;
        private readonly string? _edited;
        private readonly CopiedSheet? _copied;

        internal SourceLinks(Workbook workbook, Workbook source, CopiedSheet? copied = null)
        {
            _links = workbook.Worksheets.ExternalLinks;
            _existing = _links.Count;
            _source = source.FileName;
            _edited = FullPath(workbook.FileName, null);
            _copied = copied;
        }

        public void Dispose()
        {
            // The links back to the edited workbook name its sheets as they are, so they are made
            // local before the destination takes the source sheet's name.
            var toSource = new List<int>();
            for (int index = _links.Count - 1; index >= _existing; index--)
            {
                string target = _links[index].DataSource;
                if (FilePaths.Same(target, _source))
                {
                    toSource.Add(index);
                }
                else if (LinksToEdited(target))
                {
                    _links.RemoveAt(index, updateReferencesAsLocal: true);
                    toSource = [.. toSource.Select(static later => later - 1)];
                }
            }

            if (toSource.Count == 0)
            {
                return;
            }

            using (_copied?.NameDestinationAsSource())
            {
                foreach (int index in toSource)
                {
                    _links.RemoveAt(index, updateReferencesAsLocal: true);
                }
            }
        }

        // CELLS-COPY-SELF-LINK: a link stored relative to the source's folder, as Excel stores
        // one to a workbook beside it, names the file it has from that folder. The paths are
        // compared as text only: a link target comes from the document, and touching the file
        // it names, such as one on a network share, would let the document cause a request.
        private bool LinksToEdited(string target) =>
            _edited is not null
            && FullPath(target, Path.GetDirectoryName(FullPath(_source, null))) is { } linked
            && FilePaths.Same(Path.TrimEndingDirectorySeparator(linked), Path.TrimEndingDirectorySeparator(_edited));

        private static string? FullPath(string? path, string? folder)
        {
            if (string.IsNullOrEmpty(path) || path.Contains("://", StringComparison.Ordinal))
            {
                return null;
            }

            try
            {
                return folder is null || Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(path, folder);
            }
            catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return null;
            }
        }
    }
}
