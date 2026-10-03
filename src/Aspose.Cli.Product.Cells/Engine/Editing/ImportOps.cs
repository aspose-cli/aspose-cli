using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Contracts.Addressing;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
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
    IReadOnlyDictionary<string, string>? secrets) : IDisposable
{
    private readonly Dictionary<string, LoadedWorkbook> _opened = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Warning> _imports = [];

    internal ResourceBudgetLedger Budgets => budgets;

    /// <summary>
    /// The warnings of the opened sources, such as resources they could not load, and of the
    /// imports that read them.
    /// </summary>
    internal IReadOnlyList<Warning>? Warnings() =>
        EnvelopeParts.CombineWarnings([.. _opened.Values.Select(static loaded => loaded.Warnings()), _imports]);

    internal void Warn(Warning warning) => _imports.Add(warning);

    internal Workbook Open(string path, string? passwordEnv)
    {
        if (!_opened.TryGetValue(path, out LoadedWorkbook? loaded))
        {
            try
            {
                loaded = loader.Open(path, OperationSecrets.Resolve(secrets, passwordEnv));
            }
            catch (CliException error) when (CliErrors.IsPasswordError(error))
            {
                // The loader names the command's password option; a source's comes from its operation.
                throw CliErrors.ForOperationSource(error, "passwordEnv");
            }

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
    public static long ImportRange(Worksheet sheet, ImportRangeOp op, CellsImportSources sources)
    {
        Workbook source = sources.Open(op.Path, op.PasswordEnv);
        RangeSpec spec = A1.ParseRange(op.From);
        Worksheet fromSheet = spec.SheetName is { } sheetName ? Sheets.Resolve(source, sheetName) : source.Worksheets[0];
        RangeRef from = spec.Range;
        (Worksheet toSheet, RangeRef to) = Sheets.ResolveRange(sheet, op.To);

        CellsRange origin = fromSheet.Cells.CreateRange(
            from.Start.Row, from.Start.Column, from.RowCount, from.ColumnCount);
        CellsRange destination = toSheet.Cells.CreateRange(
            to.Start.Row, to.Start.Column, from.RowCount, from.ColumnCount);
        bool everything = op.Content == ImportContents.Everything;
        using (new SourceLinks(toSheet.Workbook, source))
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

        return from.CellCount;
    }

    public static long? ImportSheet(Workbook workbook, ImportSheetOp op, CellsImportSources sources)
    {
        Workbook source = sources.Open(op.Path, op.PasswordEnv);
        Worksheet origin = op.Sheet is { } sheetName ? Sheets.Resolve(source, sheetName) : source.Worksheets[0];
        string name = op.Name ?? origin.Name;
        if (Sheets.Names(workbook).FirstOrDefault(existing => string.Equals(existing, name, StringComparison.OrdinalIgnoreCase)) is { } taken)
        {
            throw new OperationInvalidException(
                $"the workbook already has a sheet named '{taken}'",
                "Give the imported sheet another name with \"name\", or rename or delete the existing sheet first.");
        }

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
            if (op.Position is { } position)
            {
                sheet.MoveTo(position);
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
        return 1;
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
        var files = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
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

            string[] linked = (cell.GetPrecedents()?.Cast<ReferredArea>() ?? [])
                .Where(static precedent => precedent.IsExternalLink)
                .Select(static precedent => Path.GetFileName(precedent.ExternalFileName))
                .ToArray();
            if (linked.Length == 0
                || to.Cells.CheckCell(cell.Row + rowOffset, cell.Column + columnOffset) is not { IsFormula: true } copied)
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
        sources.Warn(new Warning
        {
            Code = CellsDiagnostics.ExternalLinkCacheMissing,
            Message = $"{changed.Count} imported formula(s) on '{to.Name}' ({cells}) read {string.Join(", ", files)} through a link "
                + "that caches no values: in the source they show an error such as #REF!, but here they read the linked cells as empty, "
                + "usually as 0, and so do the formulas that depend on them.",
            Hint = "Recalculation never opens the linked workbook. Replace these formulas with set_formula or set_values, or open the "
                + "source in Excel with the linked workbook available and save it so the link caches its values.",
            Docs = "cells/editing",
            Location = sheet + "!" + changed[0],
        });
    }

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

    // Visible, workbook-scoped names; hidden and _xl names are the engine's and Excel's own.
    private static IEnumerable<Name> WorkbookNames(Workbook workbook) =>
        workbook.Worksheets.Names.Cast<Name>().Where(static name =>
            name.SheetIndex == 0 && name.IsVisible && !name.Text.StartsWith("_xl", StringComparison.OrdinalIgnoreCase));

    private static bool RefersOnlyTo(Name name, Worksheet sheet) =>
        name.GetReferredAreas(true) is { Length: > 0 } areas
        && areas.All(area => !area.IsExternalLink
            && string.Equals(area.SheetName, sheet.Name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Copying from another workbook turns a reference to a source sheet into a link to the
    /// source file. Disposing removes the links the copy added and makes their references local:
    /// they point at the sheet of the same name in this workbook, or become #REF!.
    /// </summary>
    private readonly struct SourceLinks : IDisposable
    {
        private readonly ExternalLinkCollection _links;
        private readonly int _existing;
        private readonly string _source;

        internal SourceLinks(Workbook workbook, Workbook source)
        {
            _links = workbook.Worksheets.ExternalLinks;
            _existing = _links.Count;
            _source = source.FileName;
        }

        public void Dispose()
        {
            for (int index = _links.Count - 1; index >= _existing; index--)
            {
                if (string.Equals(_links[index].DataSource, _source, StringComparison.OrdinalIgnoreCase))
                {
                    _links.RemoveAt(index, updateReferencesAsLocal: true);
                }
            }
        }
    }
}
