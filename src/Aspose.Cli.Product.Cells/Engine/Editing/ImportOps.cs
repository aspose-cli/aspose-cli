using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Contracts.Addressing;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Contracts;
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

    internal ResourceBudgetLedger Budgets => budgets;

    /// <summary>The warnings of the opened sources, such as resources they could not load.</summary>
    internal IReadOnlyList<Warning>? Warnings() =>
        EnvelopeParts.CombineWarnings([.. _opened.Values.Select(static loaded => loaded.Warnings())]);

    internal Workbook Open(string path, string? passwordEnv)
    {
        if (!_opened.TryGetValue(path, out LoadedWorkbook? loaded))
        {
            loaded = loader.Open(path, OperationSecrets.Resolve(secrets, passwordEnv));
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
        sources.Budgets.Consume(CellsBudgetDomains.Cells, origin.Cells.Count, "items", "edit");

        Worksheet sheet = workbook.Worksheets[workbook.Worksheets.Add()];
        try
        {
            sheet.Name = name;
            using (new SourceLinks(workbook, source))
            {
                sheet.Copy(origin, new CopyOptions());
            }

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

        return null;
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
