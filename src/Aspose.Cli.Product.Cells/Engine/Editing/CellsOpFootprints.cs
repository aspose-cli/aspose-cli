using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>
/// Where each operation lands in the workbook and what checks it: one method per operation, so
/// a new operation does not compile until it is placed. The target is mapped at the granularity
/// that is useful to look at — a range where the operation has one, a cell for single-cell
/// operations, the whole sheet for structural changes (A1 deliberately rejects whole-row/column
/// ranges, so a row or column band has no range representation), and none for workbook-level
/// operations, or an embedded object or a removal that names no sheet, with no visible spot to
/// focus.
/// </summary>
internal sealed class CellsOpFootprints : ICellsOpHandler<CellsOpTraits>
{
    private static readonly CellsOpFootprints Instance = new();

    private CellsOpFootprints()
    {
    }

    /// <summary>The traits of <paramref name="operation"/>.</summary>
    internal static CellsOpTraits Of(CellsOp operation) => operation.Accept(Instance);

    // ----- cell values and formulas: the diff compares them ----------------------------------

    // An anchor cell spans the matrix written from it.
    public CellsOpTraits Apply(SetValuesOp operation) => Diffed(operation.Sheet, MatrixRange(operation.Range, operation.Values)) with { WritesContents = true };

    public CellsOpTraits Apply(SetFormulaOp operation) => Diffed(operation.Sheet, operation.Range) with { WritesContents = true };

    public CellsOpTraits Apply(ClearRangeOp operation) =>
        Diffed(operation.Sheet, operation.Range) with { WritesContents = operation.What == CellsClearTargets.Contents };

    // Without a range it replaces anywhere in the sheet.
    public CellsOpTraits Apply(ReplaceTextOp operation) => Diffed(operation.Sheet, operation.Range);

    public CellsOpTraits Apply(SortRangeOp operation) => Diffed(operation.Sheet, operation.Range);

    public CellsOpTraits Apply(DeleteDuplicatesOp operation) => Diffed(operation.Sheet, operation.Range);

    // A copy changes its destination and is charged for its source; the destination anchor may
    // be sheet-qualified and then overrides the operation's sheet.
    public CellsOpTraits Apply(CopyRangeOp operation) =>
        new(CopyTarget(operation.Sheet, operation.To, from: null), Diffed: true, CellsProtectionScope.Sheet) { Source = operation.From };

    // An import spotlights the whole range it wrote.
    public CellsOpTraits Apply(ImportRangeOp operation) =>
        new(CopyTarget(operation.Sheet, operation.To, operation.From), Diffed: true, CellsProtectionScope.Sheet) { Source = operation.From };

    // ----- formatting and objects over a range ------------------------------------------------

    public CellsOpTraits Apply(FormatRangeOp operation) => Unseen(operation.Sheet, operation.Range);

    public CellsOpTraits Apply(SetBordersOp operation) => Unseen(operation.Sheet, operation.Range);

    public CellsOpTraits Apply(MergeCellsOp operation) => Unseen(operation.Sheet, operation.Range);

    public CellsOpTraits Apply(UnmergeCellsOp operation) => Unseen(operation.Sheet, operation.Range);

    public CellsOpTraits Apply(SetValidationOp operation) => Unseen(operation.Sheet, Bounds(operation.Range));

    public CellsOpTraits Apply(ClearValidationOp operation) => Unseen(operation.Sheet, Bounds(operation.Range));

    public CellsOpTraits Apply(AddConditionalFormatOp operation) => Unseen(operation.Sheet, Bounds(operation.Range));

    public CellsOpTraits Apply(ClearConditionalFormatsOp operation) => Unseen(operation.Sheet, Bounds(operation.Range));

    // A sparkline's visible change is the location strip it draws into, not the data it reads.
    public CellsOpTraits Apply(AddSparklineOp operation) => Unseen(operation.Sheet, Bounds(operation.At));

    public CellsOpTraits Apply(DefineTableOp operation) => Unseen(operation.Sheet, operation.Range);

    public CellsOpTraits Apply(ResizeTableOp operation) => Unseen(operation.Sheet, operation.Range);

    public CellsOpTraits Apply(SetAutoFilterOp operation) => Unseen(operation.Sheet, operation.Range);

    // Print settings stay open on a protected sheet.
    public CellsOpTraits Apply(SetPrintAreaOp operation) => Unseen(operation.Sheet, Bounds(operation.Range)) with { Protection = CellsProtectionScope.None };

    // ----- single cells -----------------------------------------------------------------------

    public CellsOpTraits Apply(AddCommentOp operation) => Unseen(operation.Sheet, operation.Cell);

    public CellsOpTraits Apply(SetCommentOp operation) => Unseen(operation.Sheet, operation.Cell);

    public CellsOpTraits Apply(DeleteCommentOp operation) => Unseen(operation.Sheet, operation.Cell);

    public CellsOpTraits Apply(SetHyperlinkOp operation) => Unseen(operation.Sheet, operation.Cell);

    public CellsOpTraits Apply(DeleteHyperlinkOp operation) => Unseen(operation.Sheet, operation.Cell);

    // ----- rows and columns: they shift everything below or to the right, and A1 has no
    // whole-row/column form, so they spotlight the sheet -----------------------------------------

    public CellsOpTraits Apply(InsertRowsOp operation) => Diffed(operation.Sheet, range: null);

    public CellsOpTraits Apply(DeleteRowsOp operation) => Diffed(operation.Sheet, range: null);

    public CellsOpTraits Apply(InsertColumnsOp operation) => Diffed(operation.Sheet, range: null);

    public CellsOpTraits Apply(DeleteColumnsOp operation) => Diffed(operation.Sheet, range: null);

    public CellsOpTraits Apply(ResizeRowsOp operation) => Unseen(operation.Sheet, range: null);

    public CellsOpTraits Apply(ResizeColumnsOp operation) => Unseen(operation.Sheet, range: null);

    public CellsOpTraits Apply(GroupRowsOp operation) => Unseen(operation.Sheet, range: null);

    public CellsOpTraits Apply(UngroupRowsOp operation) => Unseen(operation.Sheet, range: null);

    public CellsOpTraits Apply(GroupColumnsOp operation) => Unseen(operation.Sheet, range: null);

    public CellsOpTraits Apply(UngroupColumnsOp operation) => Unseen(operation.Sheet, range: null);

    // ----- sheets: the sheet as a whole, for an add or rename the one that exists after it ----

    public CellsOpTraits Apply(InsertSheetOp operation) =>
        new(new CellsEditTarget(operation.Name, null), Diffed: true, CellsProtectionScope.Structure)
        {
            WorkbookAddress = CellsAddresses.OfSheet("sheet", operation.Name),
        };

    // An imported sheet is named after its source sheet unless it is given a name; the source's
    // first sheet is known only once the source is open.
    public CellsOpTraits Apply(ImportSheetOp operation) =>
        new(new CellsEditTarget(operation.Name ?? operation.SourceSheet, null), Diffed: true, CellsProtectionScope.Structure)
        {
            WorkbookAddress = CellsAddresses.OfSheet("sheet", operation.Name ?? operation.SourceSheet ?? Path.GetFileNameWithoutExtension(operation.Path)),
        };

    public CellsOpTraits Apply(RenameSheetOp operation) => Diffed(operation.Name, range: null) with { Protection = CellsProtectionScope.Structure };

    public CellsOpTraits Apply(DeleteSheetOp operation) => Diffed(operation.Sheet, range: null) with { Protection = CellsProtectionScope.Structure };

    public CellsOpTraits Apply(MoveSheetOp operation) => Unseen(operation.Sheet, range: null) with { Protection = CellsProtectionScope.Structure };

    public CellsOpTraits Apply(SetSheetVisibilityOp operation) => Unseen(operation.Sheet, range: null) with { Protection = CellsProtectionScope.Structure };

    public CellsOpTraits Apply(SetTabColorOp operation) => Unseen(operation.Sheet, range: null) with { Protection = CellsProtectionScope.Structure };

    // Views and print settings stay open on a protected sheet.
    public CellsOpTraits Apply(SetActiveSheetOp operation) => Unseen(operation.Sheet, range: null) with { Protection = CellsProtectionScope.None };

    public CellsOpTraits Apply(SetSheetViewOp operation) => Unseen(operation.Sheet, range: null) with { Protection = CellsProtectionScope.None };

    public CellsOpTraits Apply(FreezePanesOp operation) => Unseen(operation.Sheet, range: null) with { Protection = CellsProtectionScope.None };

    public CellsOpTraits Apply(SetPageSetupOp operation) => Unseen(operation.Sheet, range: null) with { Protection = CellsProtectionScope.None };

    public CellsOpTraits Apply(ProtectSheetOp operation) => Unseen(operation.Sheet, range: null);

    // Unprotecting checks the password.
    public CellsOpTraits Apply(UnprotectSheetOp operation) => Unseen(operation.Sheet, range: null) with { Protection = CellsProtectionScope.None };

    // ----- removals and embedded objects: their sheet when they name one ----------------------

    public CellsOpTraits Apply(ClearTabColorOp operation) => NamedSheet(operation);

    public CellsOpTraits Apply(UnfreezePanesOp operation) => NamedSheet(operation);

    public CellsOpTraits Apply(ClearAutoFilterOp operation) => NamedSheet(operation);

    public CellsOpTraits Apply(ClearPrintAreaOp operation) => NamedSheet(operation);

    public CellsOpTraits Apply(InsertChartOp operation) => NamedSheet(operation);

    public CellsOpTraits Apply(SetChartOp operation) => NamedSheet(operation);

    public CellsOpTraits Apply(DeleteChartOp operation) => NamedSheet(operation);

    public CellsOpTraits Apply(InsertPivotOp operation) => NamedSheet(operation);

    public CellsOpTraits Apply(RefreshPivotOp operation) => NamedSheet(operation);

    public CellsOpTraits Apply(InsertImageOp operation) => NamedSheet(operation);

    // ----- the workbook: no visible spot to focus ---------------------------------------------

    public CellsOpTraits Apply(DefineNameOp operation) => Workbook(CellsAddresses.Name(operation.Name), CellsProtectionScope.None);

    public CellsOpTraits Apply(DeleteNameOp operation) => Workbook(CellsAddresses.Name(operation.Name), CellsProtectionScope.None);

    // set_default_font is workbook-scoped.
    public CellsOpTraits Apply(SetDefaultFontOp operation) => Workbook("workbook/default-font", CellsProtectionScope.None);

    // Protecting the structure again replaces its protection.
    public CellsOpTraits Apply(ProtectWorkbookOp operation) => Workbook("workbook/protection", CellsProtectionScope.Structure);

    public CellsOpTraits Apply(UnprotectWorkbookOp operation) => Workbook("workbook/protection", CellsProtectionScope.None);

    private static CellsOpTraits Diffed(string? sheet, string? range) =>
        new(new CellsEditTarget(sheet, range), Diffed: true, CellsProtectionScope.Sheet);

    private static CellsOpTraits Unseen(string? sheet, string? range) =>
        new(new CellsEditTarget(sheet, range), Diffed: false, CellsProtectionScope.Sheet);

    // Without an explicit sheet there is nothing precise enough to point at.
    private static CellsOpTraits NamedSheet(CellsSheetOp operation) =>
        new(operation.Sheet is null ? null : new CellsEditTarget(operation.Sheet, null), Diffed: false, CellsProtectionScope.Sheet);

    private static CellsOpTraits Workbook(string address, CellsProtectionScope protection) =>
        new(Target: null, Diffed: false, protection) { WorkbookAddress = address };

    /// <summary>
    /// The destination of a copy: its anchor, or the source range's size at the anchor when
    /// <paramref name="from"/> is given, clipped to the grid.
    /// </summary>
    private static CellsEditTarget CopyTarget(string? sheet, string to, string? from)
    {
        try
        {
            RangeSpec destination = A1.ParseRange(to);
            RangeRef landed = destination.Range;
            if (from is not null)
            {
                RangeRef source = A1.ParseRange(from).Range;
                CellRef anchor = landed.Start;
                landed = new RangeRef(anchor, new CellRef(
                    Math.Min(anchor.Row + source.RowCount, A1.MaxRows) - 1,
                    Math.Min(anchor.Column + source.ColumnCount, A1.MaxColumns) - 1));
            }

            return new CellsEditTarget(destination.SheetName ?? sheet, A1.FormatRange(landed));
        }
        catch (CliException)
        {
            // Best effort: an unparsable destination (conceivable under
            // --best-effort) still hints at the op's sheet.
            return new CellsEditTarget(sheet, null);
        }
    }

    /// <summary>
    /// The range <paramref name="values"/> fill from the top-left cell of <paramref name="range"/>,
    /// clipped to the grid; the range as given when it is unparsable or the matrix is empty, which
    /// only a failed operation under --best-effort can carry.
    /// </summary>
    private static string MatrixRange(string range, IReadOnlyList<IReadOnlyList<object?>> values)
    {
        if (values is not [{ Count: > 0 } first, ..])
        {
            return range;
        }

        try
        {
            CellRef anchor = A1.ParseRange(range).Range.Start;
            return A1.FormatRange(new RangeRef(anchor, new CellRef(
                Math.Min(anchor.Row + values.Count, A1.MaxRows) - 1,
                Math.Min(anchor.Column + first.Count, A1.MaxColumns) - 1)));
        }
        catch (CliException)
        {
            return range;
        }
    }

    /// <summary>
    /// The one range that covers a list of cells and ranges, such as D5:D8 for D5,D7,D8; the
    /// list as given when it is unparsable, which only a failed operation can carry.
    /// </summary>
    private static string Bounds(string list)
    {
        try
        {
            IReadOnlyList<RangeRef> parts = A1.ParseRanges(list);
            return A1.FormatRange(new RangeRef(
                new CellRef(parts.Min(static part => part.Start.Row), parts.Min(static part => part.Start.Column)),
                new CellRef(parts.Max(static part => part.End.Row), parts.Max(static part => part.End.Column))));
        }
        catch (CliException)
        {
            return list;
        }
    }
}

/// <summary>What one operation addresses and what checks it.</summary>
/// <param name="Target">
/// Where the operation lands: a null sheet means the sheet the operation ran on, a null range the
/// sheet as a whole; null when it has no visible spot to focus.
/// </param>
/// <param name="Diffed">Whether the verification diff, which compares cell values, formulas and sheets, sees what the operation does.</param>
/// <param name="Protection">What the operation goes through when it is protected.</param>
internal sealed record CellsOpTraits(CellsEditTarget? Target, bool Diffed, CellsProtectionScope Protection)
{
    /// <summary>The range a copy or import reads, charged against the cells budget instead of its target.</summary>
    public string? Source { get; init; }

    /// <summary>What an operation that names no sheet changes in the workbook, as an address.</summary>
    public string? WorkbookAddress { get; init; }

    /// <summary>Whether the operation changes only the contents of its cells, which Excel allows in unlocked cells of a protected sheet.</summary>
    public bool WritesContents { get; init; }
}

/// <summary>What protection an operation goes through.</summary>
internal enum CellsProtectionScope
{
    /// <summary>None: Excel leaves it open on a protected sheet, or it checks the password.</summary>
    None,

    /// <summary>The workbook structure: adding, removing, renaming, moving and hiding sheets.</summary>
    Structure,

    /// <summary>The protection of the sheet it changes.</summary>
    Sheet,
}
