using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cells.Drawing;
using Aspose.Cells.Pivot;
using Aspose.Cells.Tables;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>
/// The objects of a workbook one operation can change, as the SDK snapshots them around the
/// operation (<see cref="IOperationModel{TOp}.Snapshot"/>), each at its <see cref="CellsAddresses"/>
/// address and in the state a read reports it (<see cref="ObjectProjection"/>,
/// <see cref="SheetLayoutProjection"/>, <see cref="InfoProjection"/>), so that what an operation
/// counts is what a read of its output shows changed:
/// <list type="bullet">
/// <item>a cell (<c>cell/Sales!B2</c>) by its stored value, formula and style; an empty cell with the default style is absent;</item>
/// <item>the sheets in tab order (<c>sheet/Sales</c>) by identity and name, a sheet's visibility, tab color, view, protection,
/// frozen panes, AutoFilter, print area and page setup (<c>sheet/Sales</c>, <c>tab-color/Sales</c>, <c>sheet-view/Sales</c>,
/// <c>protection/Sales</c>, <c>freeze-panes/Sales</c>, <c>autofilter/Sales</c>, <c>print-area/Sales</c>, <c>page-setup/Sales</c>);</item>
/// <item>rows and columns (<c>row/Sales!5</c>, <c>column/Sales!C</c>) by height or width, outline level and visibility, and a row
/// or column an operation inserts or deletes by its being there;</item>
/// <item>merged areas (<c>merge/Sales!A1:C1</c>), comments (<c>comment/Sales!B2</c>), hyperlinks (<c>hyperlink/Sales!B2</c>)
/// and validations (<c>validation/Sales!B2:B9,D2:D9</c>) by the cells they cover;</item>
/// <item>conditional formats, sparkline groups, charts and pictures of a sheet in order
/// (<c>conditional-format/Sales/1</c>, <c>sparkline-group/Sales/1</c>, <c>chart/Sales/1</c>, <c>image/Sales/1</c>);</item>
/// <item>tables and pivot tables by name (<c>table/Sales</c>, <c>pivot/Sales/PivotTable1</c>), defined names (<c>name/TaxRate</c>);</item>
/// <item>the workbook's default font, active sheet and structure protection (<c>workbook/default-font</c>,
/// <c>workbook/active-sheet</c>, <c>workbook/protection</c>).</item>
/// </list>
/// A sheet is the one the operation names, resolved by name when the snapshot is taken. What the
/// engine keeps that a read does not report is added to the state where an operation can change
/// it: an object's identity, a picture's data, a chart's or picture's anchor, a link's screen tip,
/// a validation's prompts and the style facts a cell read leaves out.
/// </summary>
/// <param name="workbook">The workbook the batch edits.</param>
/// <param name="applied">Whether an operation was already applied: a snapshot after it, not before.</param>
internal sealed class CellsSnapshots(Workbook workbook, Func<CellsOp, bool> applied) : ICellsOpHandler<OperationSnapshot>
{
    // Most cells a range snapshot reads cell by cell; a larger range reads the cells the sheet holds.
    private const long DenseLimit = 100_000;

    private static readonly ProductJsonContext Json = ProductJsonContext.Default;

    // Identities of the objects the engine does not name, so a moved or renamed one is the same object.
    private static readonly ConditionalWeakTable<object, string> Identities = new();
    private static int _nextIdentity;

    // ----- cells ---------------------------------------------------------------------------------

    public OperationSnapshot Apply(SetValuesOp operation) => Cells(operation, Matrix(operation));

    public OperationSnapshot Apply(SetFormulaOp operation) => Cells(operation, operation.Range);

    public OperationSnapshot Apply(ClearRangeOp operation)
    {
        OperationSnapshot snapshot = Cells(operation, operation.Range);
        AddSparklineGroups(snapshot, Sheet(operation));
        return snapshot;
    }

    // A border a cell shares with its neighbor is the neighbor's too: a bottom line drawn on A1 is
    // the top line a read reports for A2.
    public OperationSnapshot Apply(FormatRangeOp operation) =>
        Cells(operation, operation.Style.Borders is null ? operation.Range : WithNeighbors(operation.Range));

    public OperationSnapshot Apply(ReplaceTextOp operation) =>
        Cells(operation, operation.Range ?? A1.FormatRange(new RangeRef(new CellRef(0, 0), new CellRef(A1.MaxRows - 1, A1.MaxColumns - 1))));

    public OperationSnapshot Apply(SetBordersOp operation) => Cells(operation, WithNeighbors(operation.Range));

    /// <summary>The range and the cells around it, whose borders it shares.</summary>
    private static string WithNeighbors(string range)
    {
        RangeRef area = A1.ParseRange(range).Range;
        return A1.FormatRange(new RangeRef(
            new CellRef(Math.Max(area.Start.Row - 1, 0), Math.Max(area.Start.Column - 1, 0)),
            new CellRef(Math.Min(area.End.Row + 1, A1.MaxRows - 1), Math.Min(area.End.Column + 1, A1.MaxColumns - 1))));
    }

    public OperationSnapshot Apply(SortRangeOp operation) => Cells(operation, operation.Range);

    public OperationSnapshot Apply(DeleteDuplicatesOp operation) => Cells(operation, operation.Range);

    public OperationSnapshot Apply(MergeCellsOp operation) => Merges(operation, operation.Range);

    public OperationSnapshot Apply(UnmergeCellsOp operation) => Merges(operation, operation.Range);

    public OperationSnapshot Apply(CopyRangeOp operation) =>
        Landing(operation, Sheets.ResolveRange(Sheet(operation), operation.From).Range, operation.To);

    public OperationSnapshot Apply(ImportRangeOp operation) =>
        Landing(operation, A1.ParseRange(operation.From).Range, operation.To);

    /// <summary>The cells and merged areas where a range of the size of <paramref name="from"/> lands at <paramref name="to"/>.</summary>
    private OperationSnapshot Landing(CellsSheetOp operation, RangeRef from, string to)
    {
        (Worksheet toSheet, RangeRef target) = Sheets.ResolveRange(Sheet(operation), to);
        var snapshot = new OperationSnapshot();
        AddCells(snapshot, toSheet, Landed(target.Start, from));
        AddMerges(snapshot, toSheet, Landed(target.Start, from));
        return snapshot;
    }

    public OperationSnapshot Apply(SetHyperlinkOp operation) => Hyperlinks(operation, operation.Cell);

    public OperationSnapshot Apply(DeleteHyperlinkOp operation) => Hyperlinks(operation, operation.Cell);

    public OperationSnapshot Apply(AddCommentOp operation) => Comments(operation);

    public OperationSnapshot Apply(SetCommentOp operation) => Comments(operation);

    public OperationSnapshot Apply(DeleteCommentOp operation) => Comments(operation);

    // ----- rows and columns ----------------------------------------------------------------------

    public OperationSnapshot Apply(InsertRowsOp operation) => Shift(operation, rows: true, operation.At - 1, operation.Count, insert: true);

    public OperationSnapshot Apply(DeleteRowsOp operation) => Shift(operation, rows: true, operation.At - 1, operation.Count, insert: false);

    public OperationSnapshot Apply(InsertColumnsOp operation) =>
        Shift(operation, rows: false, A1.ParseColumn(operation.At), operation.Count, insert: true);

    public OperationSnapshot Apply(DeleteColumnsOp operation) =>
        Shift(operation, rows: false, A1.ParseColumn(operation.At), operation.Count, insert: false);

    public OperationSnapshot Apply(ResizeRowsOp operation) => Lines(operation, rows: true, operation.First - 1, (operation.Last ?? operation.First) - 1);

    public OperationSnapshot Apply(GroupRowsOp operation) => Lines(operation, rows: true, operation.First - 1, (operation.Last ?? operation.First) - 1);

    public OperationSnapshot Apply(UngroupRowsOp operation) => Lines(operation, rows: true, operation.First - 1, (operation.Last ?? operation.First) - 1);

    public OperationSnapshot Apply(ResizeColumnsOp operation) => Columns(operation, operation.First, operation.Last);

    public OperationSnapshot Apply(GroupColumnsOp operation) => Columns(operation, operation.First, operation.Last);

    public OperationSnapshot Apply(UngroupColumnsOp operation) => Columns(operation, operation.First, operation.Last);

    // ----- sheets ----------------------------------------------------------------------------------

    public OperationSnapshot Apply(InsertSheetOp operation) => SheetOrder();

    public OperationSnapshot Apply(ImportSheetOp operation) => SheetOrder();

    // The sheet keeps its place and identity: one change, at the new name.
    public OperationSnapshot Apply(RenameSheetOp operation) =>
        One(CellsAddresses.OfSheet("sheet", operation.Name), RenamedSheet(operation)?.Name ?? "-");

    // Before the rename the sheet has its old name, or is the active one; after it, the new name.
    private Worksheet? RenamedSheet(RenameSheetOp operation) =>
        operation.Sheet is { } old && workbook.Worksheets[old] is { } found
            ? found
            : workbook.Worksheets[operation.Name] ?? (operation.Sheet is null ? workbook.Worksheets[workbook.Worksheets.ActiveSheetIndex] : null);

    public OperationSnapshot Apply(DeleteSheetOp operation) => SheetOrder();

    // The moved sheet counts once, at its new place.
    public OperationSnapshot Apply(MoveSheetOp operation) => SheetOrder(Sheet(operation));

    // Hiding the active sheet activates another one.
    public OperationSnapshot Apply(SetSheetVisibilityOp operation) =>
        Of(operation, "sheet", static sheet => Sheets.Visibility(sheet))
            .Add("workbook/active-sheet", workbook.Worksheets[workbook.Worksheets.ActiveSheetIndex].Name);

    // A read does not report the active sheet.
    public OperationSnapshot Apply(SetActiveSheetOp operation) =>
        One("workbook/active-sheet", workbook.Worksheets[workbook.Worksheets.ActiveSheetIndex].Name);

    public OperationSnapshot Apply(SetTabColorOp operation) => TabColor(operation);

    public OperationSnapshot Apply(ClearTabColorOp operation) => TabColor(operation);

    public OperationSnapshot Apply(SetSheetViewOp operation) =>
        Of(operation, "sheet-view", static sheet => Read(SheetLayoutProjection.SheetView(sheet), Json.CellsSheetViewInfo));

    public OperationSnapshot Apply(FreezePanesOp operation) => FreezePanes(operation);

    public OperationSnapshot Apply(UnfreezePanesOp operation) => FreezePanes(operation);

    public OperationSnapshot Apply(SetAutoFilterOp operation) => AutoFilter(operation);

    public OperationSnapshot Apply(ClearAutoFilterOp operation) => AutoFilter(operation);

    public OperationSnapshot Apply(SetPrintAreaOp operation) => PrintArea(operation);

    public OperationSnapshot Apply(ClearPrintAreaOp operation) => PrintArea(operation);

    // The paper, scale and header texts as the engine keeps them: a read reports the paper's size,
    // only the scale that applies, and no empty text.
    public OperationSnapshot Apply(SetPageSetupOp operation) =>
        Of(operation, "page-setup", static sheet => PageSetup(sheet.PageSetup));

    private static string PageSetup(PageSetup page) => Join(Read(SheetLayoutProjection.PageSetup(page), Json.CellsPageSetupInfo),
        page.PaperSize, page.PaperWidth, page.PaperHeight, page.IsPercentScale, page.Zoom, page.FitToPagesWide, page.FitToPagesTall,
        page.GetHeader(0), page.GetHeader(1), page.GetHeader(2), page.GetFooter(0), page.GetFooter(1), page.GetFooter(2));

    public OperationSnapshot Apply(ProtectSheetOp operation) => SheetProtection(operation);

    public OperationSnapshot Apply(UnprotectSheetOp operation) => SheetProtection(operation);

    public OperationSnapshot Apply(ProtectWorkbookOp operation) => WorkbookProtection();

    public OperationSnapshot Apply(UnprotectWorkbookOp operation) => WorkbookProtection();

    public OperationSnapshot Apply(SetDefaultFontOp operation) =>
        One("workbook/default-font", Read(InfoProjection.DefaultFont(workbook), Json.CellsDefaultFontInfo));

    // ----- names and objects ---------------------------------------------------------------------

    public OperationSnapshot Apply(DefineNameOp operation) => Names();

    public OperationSnapshot Apply(DeleteNameOp operation) => Names();

    public OperationSnapshot Apply(SetValidationOp operation) => Validations(operation);

    public OperationSnapshot Apply(ClearValidationOp operation) => Validations(operation);

    public OperationSnapshot Apply(AddConditionalFormatOp operation) => ConditionalFormats(operation);

    public OperationSnapshot Apply(ClearConditionalFormatsOp operation) => ConditionalFormats(operation);

    public OperationSnapshot Apply(AddSparklineOp operation) => AddSparklineGroups(new OperationSnapshot(), Sheet(operation));

    public OperationSnapshot Apply(DefineTableOp operation) => Tables(operation, new OperationSnapshot());

    // The engine fills a table's calculated columns into the rows it takes in and moves its totals
    // row, so the cells of the rows the table had or has count too: the snapshot before the
    // operation keeps the last of them for the one after it.
    public OperationSnapshot Apply(ResizeTableOp operation)
    {
        RangeRef range = A1.ParseRange(operation.Range).Range;
        if (!applied(operation))
        {
            int lastRow = TableOps.Find(Sheet(operation), operation.Name) is { } table ? Math.Max(table.EndRow, range.End.Row) : range.End.Row;
            _resized = (operation, lastRow);
        }
        else if (_resized is not { } resized || !ReferenceEquals(resized.Operation, operation))
        {
            throw new InvalidOperationException("The snapshot after an operation is taken after the snapshot before it.");
        }

        var rows = new RangeRef(range.Start, new CellRef(_resized.Value.LastRow, range.End.Column));
        return Tables(operation, Cells(operation, A1.FormatRange(rows)));
    }

    // The resize_table being snapshot and the last row of the table before it or after it.
    private (CellsOp Operation, int LastRow)? _resized;

    private OperationSnapshot Tables(CellsSheetOp operation, OperationSnapshot snapshot)
    {
        Worksheet sheet = Sheet(operation);
        foreach (ListObject table in sheet.ListObjects)
        {
            CellsTableInfo read = ObjectProjection.Table(sheet, table);
            snapshot.Add(CellsAddresses.Table(read.Name), Join(Read(read, Json.CellsTableInfo), table.TableStyleType, table.TableStyleName));
        }

        return snapshot;
    }

    public OperationSnapshot Apply(InsertChartOp operation) => Charts(operation);

    public OperationSnapshot Apply(SetChartOp operation) => Charts(operation);

    public OperationSnapshot Apply(DeleteChartOp operation) => Charts(operation);

    public OperationSnapshot Apply(InsertPivotOp operation) => Pivots(operation);

    public OperationSnapshot Apply(RefreshPivotOp operation) => Pivots(operation);

    // A picture by its identity and data too, so one replaced with another of the same size counts,
    // and not by its place in the sheet's list, which its address names.
    public OperationSnapshot Apply(InsertImageOp operation)
    {
        Worksheet sheet = Sheet(operation);
        return new OperationSnapshot().AddSequence(CellsAddresses.Sequence("image", sheet.Name), Enumerable.Range(0, sheet.Pictures.Count)
            .Select(index => (CellsAddresses.Indexed("image", sheet.Name, index),
                Join(Identity(sheet.Pictures[index]), Convert.ToHexString(SHA256.HashData(sheet.Pictures[index].Data ?? [])),
                    sheet.Pictures[index].UpperLeftRow, sheet.Pictures[index].UpperLeftColumn, sheet.Pictures[index].Width, sheet.Pictures[index].Height,
                    Read(ObjectProjection.Image(sheet, sheet.Pictures[index], index + 1) with { Index = 0 }, Json.CellsImageInfo)))));
    }

    // ----- helpers ---------------------------------------------------------------------------------

    private Worksheet Sheet(CellsSheetOp operation) => Sheets.Resolve(workbook, operation);

    /// <summary>The one object of a unit the operation's sheet has, in its state.</summary>
    private OperationSnapshot Of(CellsSheetOp operation, string unit, Func<Worksheet, string> state)
    {
        Worksheet sheet = Sheet(operation);
        return One(CellsAddresses.OfSheet(unit, sheet.Name), state(sheet));
    }

    private OperationSnapshot Cells(CellsSheetOp operation, string range)
    {
        var snapshot = new OperationSnapshot();
        AddCells(snapshot, Sheet(operation), A1.ParseRange(range).Range);
        return snapshot;
    }

    private OperationSnapshot Merges(CellsSheetOp operation, string range)
    {
        Worksheet sheet = Sheet(operation);
        RangeRef area = A1.ParseRange(range).Range;
        var snapshot = new OperationSnapshot();
        AddCells(snapshot, sheet, area);
        AddMerges(snapshot, sheet, area);
        return snapshot;
    }

    private static string Matrix(SetValuesOp operation)
    {
        CellRef anchor = A1.ParseRange(operation.Range).Range.Start;
        int columns = operation.Values.Max(static row => row.Count);
        return A1.FormatRange(new RangeRef(anchor, new CellRef(
            Math.Min(anchor.Row + operation.Values.Count, A1.MaxRows) - 1,
            Math.Min(anchor.Column + Math.Max(columns, 1), A1.MaxColumns) - 1)));
    }

    private static RangeRef Landed(CellRef anchor, RangeRef source) => new(anchor, new CellRef(
        Math.Min(anchor.Row + source.RowCount, A1.MaxRows) - 1,
        Math.Min(anchor.Column + source.ColumnCount, A1.MaxColumns) - 1));

    /// <summary>Every cell of <paramref name="range"/> that holds a value, a formula or a style other than the default.</summary>
    private void AddCells(OperationSnapshot snapshot, Worksheet sheet, RangeRef range)
    {
        string blank = StyleKey(workbook.DefaultStyle);
        IEnumerable<Cell> cells = range.CellCount <= DenseLimit
            ? Dense(sheet, range)
            : sheet.Cells.Cast<Cell>().Where(cell => Inside(cell.Row, cell.Column, range));
        foreach (Cell cell in cells)
        {
            string state = ComparisonValue.Stored(cell);
            string style = StyleKey(cell.GetStyle());
            if (state.Length > 0 || style != blank)
            {
                snapshot.Add(CellsAddresses.Cell(sheet.Name, cell.Row, cell.Column), state + "|" + style);
            }
        }
    }

    private static IEnumerable<Cell> Dense(Worksheet sheet, RangeRef range)
    {
        for (int row = range.Start.Row; row <= range.End.Row; row++)
        {
            for (int column = range.Start.Column; column <= range.End.Column; column++)
            {
                if (sheet.Cells.CheckCell(row, column) is { } cell)
                {
                    yield return cell;
                }
            }
        }
    }

    private static bool Inside(int row, int column, RangeRef range) =>
        row >= range.Start.Row && row <= range.End.Row && column >= range.Start.Column && column <= range.End.Column;

    private static void AddMerges(OperationSnapshot snapshot, Worksheet sheet, RangeRef range)
    {
        foreach (CellArea area in sheet.Cells.GetMergedAreas() ?? [])
        {
            if (area.StartRow <= range.End.Row && area.EndRow >= range.Start.Row
                && area.StartColumn <= range.End.Column && area.EndColumn >= range.Start.Column)
            {
                snapshot.Add(CellsAddresses.Area("merge", sheet.Name, area), "merged");
            }
        }
    }

    /// <summary>
    /// A text that differs exactly when two styles format a cell differently: the style a read
    /// reports, and what the engine keeps that a read reports more coarsely or not at all (the kind
    /// of underline, colors with their transparency, the fill pattern, the number format's id and
    /// code, alignments outside the vocabulary, rotation, shrinking to fit, a hidden formula and
    /// every border's line and color), so that copying or sorting such a style still counts.
    /// </summary>
    internal static string StyleKey(Style style) => Join(Read(StylePool.Map(style), Json.CellsStyleData),
        style.Font.Underline, style.Font.Color.ToArgb(), style.Pattern, style.ForegroundColor.ToArgb(), style.BackgroundColor.ToArgb(),
        style.Number, style.Custom, style.HorizontalAlignment, style.VerticalAlignment, style.RotationAngle, style.ShrinkToFit, style.IsFormulaHidden,
        string.Join(";", ((BorderType[])[BorderType.TopBorder, BorderType.BottomBorder, BorderType.LeftBorder, BorderType.RightBorder,
            BorderType.DiagonalDown, BorderType.DiagonalUp]).Select(edge => Join(style.Borders[edge].LineStyle, style.Borders[edge].Color.ToArgb()))));

    // Links that share an address, such as two over one cell, are one object whose state is theirs
    // in order: what a read reports of each, and its address, text and screen tip as the engine keeps them.
    private OperationSnapshot Hyperlinks(CellsSheetOp operation, string cellText)
    {
        Worksheet sheet = Sheet(operation);
        CellRef cell = A1.ParseCell(cellText);
        var snapshot = new OperationSnapshot();
        AddCells(snapshot, sheet, new RangeRef(cell, cell));
        foreach (IGrouping<string, string> same in sheet.Hyperlinks.Cast<Hyperlink>()
            .Select(link => (Read: ObjectProjection.Hyperlink(sheet, link), State: Join(HyperlinkState(sheet, link), link.ScreenTip)))
            .GroupBy(static item => CellsAddresses.At("hyperlink", item.Read.Sheet, item.Read.Range), static item => item.State, StringComparer.Ordinal))
        {
            snapshot.Add(same.Key, string.Join("\u001E", same));
        }

        return snapshot;
    }

    private OperationSnapshot Comments(CellsSheetOp operation)
    {
        Worksheet sheet = Sheet(operation);
        var snapshot = new OperationSnapshot();
        foreach (Comment comment in sheet.Comments)
        {
            CellsCommentInfo read = ObjectProjection.Comment(sheet, comment);
            snapshot.Add(CellsAddresses.At("comment", read.Sheet, read.Cell), CommentState(read, comment));
        }

        return snapshot;
    }

    /// <summary>
    /// What inserting or deleting rows or columns changes: the lines it adds or removes, each on
    /// the side of the operation that has it, and every object anchored on the sheet that the
    /// engine moved, resized, split or removed, each counted once at its address in the output,
    /// or in the input for one the operation removed. Cells that only move with their line, and
    /// formulas the engine rewrites to follow them, are not counted.
    /// </summary>
    private OperationSnapshot Shift(CellsSheetOp operation, bool rows, int at, int count, bool insert)
    {
        Worksheet sheet = Sheet(operation);
        bool after = applied(operation);
        var snapshot = new OperationSnapshot();
        if (insert == after)
        {
            for (int line = at; line < at + count; line++)
            {
                snapshot.Add(CellsAddresses.Line(sheet.Name, rows, line), "line");
            }
        }

        return Tracked(operation, snapshot, AnchoredObjects(sheet), area => Moved(area, rows, at, count, insert));
    }

    /// <summary>
    /// Objects an operation can move, resize, split, replace or remove, each counted once when it
    /// changed: at its address in the output, or in the input for one the operation removed.
    /// </summary>
    /// <remarks>
    /// Which object became which is known only once the operation ran, so the snapshot before it
    /// lists the objects as they are and the one after it completes both: it matches each object
    /// with itself, by the engine's own object where it keeps one (a comment, a link, a
    /// validation, a chart), and otherwise by covering what the earlier one should cover after
    /// the operation (a merged area, or a validation the operation replaced with an equal one),
    /// and adds what the operation changed. An object left as it was is in neither, and an object
    /// removed and another moved into its place are two changes.
    /// </remarks>
    /// <param name="operation">The operation being snapshot.</param>
    /// <param name="snapshot">The snapshot to complete, before or after the operation.</param>
    /// <param name="objects">The objects as they are now.</param>
    /// <param name="moved">Where the operation should put an area, or null when it removes it.</param>
    private OperationSnapshot Tracked(CellsOp operation, OperationSnapshot snapshot, IReadOnlyList<Anchored> objects, Func<CellArea, CellArea?> moved)
    {
        if (!applied(operation))
        {
            _tracking = (operation, snapshot, objects);
            return snapshot;
        }

        if (_tracking is not { } tracking || !ReferenceEquals(tracking.Operation, operation))
        {
            throw new InvalidOperationException("The snapshot after an operation is taken after the snapshot before it.");
        }

        _tracking = null;
        string Place(Anchored item, bool predicted) => item.Areas is null ? item.Address : string.Join(",", item.Areas
            .Select(area => predicted ? moved(area) : area)
            .OfType<CellArea>()
            .Select(Where));

        // Each object after the operation with itself before it: the engine's own object first,
        // then what covers what the earlier one should.
        var before = new List<Anchored>(tracking.Objects);
        var earlier = new Anchored?[objects.Count];
        for (int pass = 0; pass < 2; pass++)
        {
            for (int index = 0; index < objects.Count; index++)
            {
                Anchored item = objects[index];
                int match = earlier[index] is not null ? -1 : pass == 0
                    ? item.Engine is null ? -1 : before.FindIndex(old => ReferenceEquals(old.Engine, item.Engine))
                    : before.FindIndex(old => old.Unit == item.Unit && Place(old, predicted: true) is { Length: > 0 } place
                        && place == Place(item, predicted: false));
                if (match >= 0)
                {
                    earlier[index] = before[match];
                    before.RemoveAt(match);
                }
            }
        }

        // Each change on its own, even at an address another change has: an object moved where
        // a removed one was, or two links over one cell, are a change each. A change is in the
        // snapshot after only and a removal in the one before only, so the two never pair up.
        var addressed = new HashSet<string>(StringComparer.Ordinal);
        var shared = new List<(string Address, string State)>();
        for (int index = 0; index < objects.Count; index++)
        {
            Anchored item = objects[index];
            if (earlier[index]?.State == item.State)
            {
                continue;
            }

            if (addressed.Add(item.Address))
            {
                snapshot.Add(item.Address, item.State);
            }
            else
            {
                shared.Add((item.Address, item.State));
            }
        }

        snapshot.AddSequence("tracked-shared", shared);
        tracking.Before.AddSequence("tracked-removed", before.Select(static item => (item.Address, item.State)));
        return snapshot;
    }

    // The objects before the operation being snapshot, and the snapshot taken before it.
    private (CellsOp Operation, OperationSnapshot Before, IReadOnlyList<Anchored> Objects)? _tracking;

    /// <summary>One object anchored on a sheet: its unit, the engine's own object where it keeps one, the areas it covers, its address and state.</summary>
    private sealed record Anchored(string Unit, object? Engine, IReadOnlyList<CellArea>? Areas, string Address, string State);

    /// <summary>
    /// Every object anchored on the sheet that inserting or deleting lines can move, resize or
    /// remove, each by where it is: what it covers, a link's or comment's content, a chart's
    /// series and a sparkline's data.
    /// </summary>
    private static List<Anchored> AnchoredObjects(Worksheet sheet)
    {
        var objects = new List<Anchored>();
        foreach (Comment comment in sheet.Comments)
        {
            CellsCommentInfo read = ObjectProjection.Comment(sheet, comment);
            objects.Add(new Anchored("comment", comment, [CellArea.CreateCellArea(comment.Row, comment.Column, comment.Row, comment.Column)],
                CellsAddresses.At("comment", sheet.Name, read.Cell), CommentState(read, comment)));
        }

        foreach (Hyperlink link in sheet.Hyperlinks)
        {
            CellsHyperlinkInfo read = ObjectProjection.Hyperlink(sheet, link);
            objects.Add(new Anchored("hyperlink", link, [link.Area], CellsAddresses.At("hyperlink", sheet.Name, read.Range), HyperlinkState(sheet, link)));
        }

        foreach (CellArea area in sheet.Cells.GetMergedAreas() ?? [])
        {
            objects.Add(new Anchored("merge", null, [area], CellsAddresses.Area("merge", sheet.Name, area), Where(area)));
        }

        foreach (Validation validation in sheet.Validations)
        {
            if (ObjectProjection.Validation(sheet, validation) is { } read)
            {
                objects.Add(new Anchored("validation", validation, validation.Areas, CellsAddresses.At("validation", sheet.Name, read.Range), read.Range));
            }
        }

        for (int index = 0; index < sheet.Charts.Count; index++)
        {
            Chart chart = sheet.Charts[index];
            objects.Add(new Anchored("chart", chart, null, CellsAddresses.Indexed("chart", sheet.Name, index),
                Join(Anchor(chart.ChartObject), string.Join(";", chart.NSeries.Cast<Series>().Select(static series => Join(series.Name, series.Values, series.XValues))))));
        }

        for (int index = 0; index < sheet.Pictures.Count; index++)
        {
            objects.Add(new Anchored("image", sheet.Pictures[index], null, CellsAddresses.Indexed("image", sheet.Name, index), Anchor(sheet.Pictures[index])));
        }

        FormatCondition[] rules = Rules(sheet);
        objects.AddRange(ObjectProjection.ConditionalFormats(sheet).Select((read, index) =>
            new Anchored("conditional-format", rules[index], null, CellsAddresses.Indexed("conditional-format", sheet.Name, index), read.Range)));

        for (int index = 0; index < sheet.SparklineGroups.Count; index++)
        {
            SparklineGroup group = sheet.SparklineGroups[index];
            objects.Add(new Anchored("sparkline-group", group, null, CellsAddresses.Indexed("sparkline-group", sheet.Name, index), Sparklines(group)));
        }

        foreach (ListObject table in sheet.ListObjects)
        {
            CellsTableInfo read = ObjectProjection.Table(sheet, table);
            objects.Add(new Anchored("table", table, null, CellsAddresses.Table(read.Name), read.Range));
        }

        foreach (PivotTable pivot in sheet.PivotTables)
        {
            CellsPivotInfo read = ObjectProjection.Pivot(sheet, pivot);
            objects.Add(new Anchored("pivot", pivot, null, CellsAddresses.Pivot(read.Sheet, read.Name), read.Range));
        }

        return objects;
    }

    // The rules of the sheet's conditional formats, in the order a read lists them.
    private static FormatCondition[] Rules(Worksheet sheet) => [.. sheet.ConditionalFormattings.Cast<FormatConditionCollection>()
        .SelectMany(static conditions => Enumerable.Range(0, conditions.Count).Select(index => conditions[index]))];

    private static string Where(CellArea area) => Join(area.StartRow, area.StartColumn, area.EndRow, area.EndColumn);

    // Where a chart or picture is anchored, which a read does not report whole.
    private static string Anchor(Shape shape) => Join(shape.UpperLeftRow, shape.UpperLeftColumn, shape.LowerRightRow, shape.LowerRightColumn);

    // A comment as a read reports it, and its author and text as the engine keeps them: a read
    // leaves out an empty author and reports no text as an empty one.
    private static string CommentState(CellsCommentInfo read, Comment comment) => Join(Read(read, Json.CellsCommentInfo), comment.Author, comment.Note);

    // A link's cells and display as a read reports them, and its address and text as the engine
    // keeps them: a read rewrites an address within the workbook as the operations take it.
    private static string HyperlinkState(Worksheet sheet, Hyperlink link) =>
        Join(Read(ObjectProjection.Hyperlink(sheet, link) with { Url = null, Target = null }, Json.CellsHyperlinkInfo), link.Address, link.TextToDisplay);

    // A sparkline group's sparklines as the engine keeps them: a read reports the block they cover.
    private static string Sparklines(SparklineGroup group) =>
        string.Join(";", group.Sparklines.Cast<Sparkline>().Select(static line => Join(line.DataRange, line.Row, line.Column)));

    /// <summary>
    /// Where an area should land once <paramref name="count"/> lines are inserted or deleted at
    /// <paramref name="at"/>: what remains of it, or null when the deletion covers it whole.
    /// </summary>
    private static CellArea? Moved(CellArea area, bool rows, int at, int count, bool insert)
    {
        (int first, int last) = rows ? (area.StartRow, area.EndRow) : (area.StartColumn, area.EndColumn);
        (first, last) = (Shifted(first, at, count, insert, start: true), Shifted(last, at, count, insert, start: false));
        if (last < first)
        {
            return null;
        }

        return rows
            ? CellArea.CreateCellArea(first, area.StartColumn, last, area.EndColumn)
            : CellArea.CreateCellArea(area.StartRow, first, area.EndRow, last);
    }

    /// <summary>
    /// Where a row or column index lands once <paramref name="count"/> lines are inserted or
    /// deleted at <paramref name="at"/>: an end inside a deleted span moves to the edge of what
    /// remains, so an area's start and end bracket what is left of it.
    /// </summary>
    private static int Shifted(int index, int at, int count, bool insert, bool start) =>
        insert
            ? (index >= at ? index + count : index)
            : index < at ? index
            : index >= at + count ? index - count
            : start ? at : at - 1;

    /// <summary>
    /// Rows or columns from <paramref name="first"/> to <paramref name="last"/> (from 0), each by its
    /// outline level and visibility as the sheet layout reads them and its height or width as the
    /// engine keeps it: a read reports a size to two decimals, and none for a fitted row or a column
    /// of the standard width.
    /// </summary>
    private OperationSnapshot Lines(CellsSheetOp operation, bool rows, int first, int last)
    {
        Worksheet sheet = Sheet(operation);
        var snapshot = new OperationSnapshot();
        for (int index = first; index <= last; index++)
        {
            SheetLayoutProjection.Line line = SheetLayoutProjection.At(sheet, rows, index);
            snapshot.Add(CellsAddresses.Line(sheet.Name, rows, index),
                Join(rows ? sheet.Cells.GetRowHeight(index) : sheet.Cells.GetColumnWidth(index), line.Level, line.Hidden));
        }

        return snapshot;
    }

    private OperationSnapshot Columns(CellsSheetOp operation, string first, string? last) =>
        Lines(operation, rows: false, A1.ParseColumn(first), A1.ParseColumn(last ?? first));

    /// <summary>The sheets in tab order, each by its identity and name, naming the sheet the operation moves.</summary>
    private OperationSnapshot SheetOrder(Worksheet? moved = null)
    {
        static string State(Worksheet sheet) => Join(Identity(sheet), sheet.Name);
        return new OperationSnapshot().AddSequence("sheet", workbook.Worksheets.Cast<Worksheet>()
            .Select(sheet => (CellsAddresses.OfSheet("sheet", sheet.Name), State(sheet))), moved is null ? null : [State(moved)]);
    }

    // The color with its transparency, which a read leaves out.
    private OperationSnapshot TabColor(CellsSheetOp operation) =>
        Of(operation, "tab-color", static sheet => Join(Read(SheetLayoutProjection.TabColor(sheet), Json.CellsTabColorInfo),
            sheet.TabColor.IsEmpty ? null : sheet.TabColor.ToArgb()));

    // The rows and columns frozen too, which a read leaves out.
    private OperationSnapshot FreezePanes(CellsSheetOp operation) =>
        Of(operation, "freeze-panes", static sheet => Join(Read(SheetLayoutProjection.FreezePanes(sheet), Json.CellsFreezePanesInfo),
            sheet.GetFreezedPanes(out _, out _, out int rows, out int columns) ? Join(rows, columns) : null));

    // The references as the engine keeps them: a read leaves out their absolute markers.
    private OperationSnapshot AutoFilter(CellsSheetOp operation) =>
        Of(operation, "autofilter", static sheet => Join(Read(SheetLayoutProjection.Autofilter(sheet), Json.CellsAutofilterInfo), sheet.AutoFilter.Range));

    private OperationSnapshot PrintArea(CellsSheetOp operation) =>
        Of(operation, "print-area", static sheet => Join(Read(SheetLayoutProjection.PrintArea(sheet.PageSetup), Json.CellsPrintAreaInfo),
            sheet.PageSetup.PrintArea, sheet.PageSetup.PrintTitleRows, sheet.PageSetup.PrintTitleColumns));

    // The password and the actions allowed as the engine keeps them on an unprotected sheet too.
    private OperationSnapshot SheetProtection(CellsSheetOp operation) =>
        Of(operation, "protection", static sheet => Join(Read(SheetLayoutProjection.Protection(sheet), Json.CellsSheetProtectionInfo),
            sheet.Protection.IsProtectedWithPassword, sheet.IsProtected ? null : string.Join(",", ProtectOps.Allowed(sheet.Protection))));

    // The kind of protection and its password as the engine keeps them: a read reports the structure's only.
    private OperationSnapshot WorkbookProtection() =>
        One("workbook/protection", Join(Sheets.StructureProtected(workbook), Sheets.StructurePasswordProtected(workbook),
            workbook.Settings.ProtectionType, workbook.IsWorkbookProtectedWithPassword));

    private OperationSnapshot Names()
    {
        var snapshot = new OperationSnapshot();
        // Every name, the engine's own _xl names too; a broken name's reference, which a read
        // reports as #REF!, is empty.
        foreach (Name name in workbook.Worksheets.Names)
        {
            CellsDefinedNameInfo read = DefinedNames.Info(name);
            snapshot.Add(CellsAddresses.Name(read.Name, name.SheetIndex > 0 ? workbook.Worksheets[name.SheetIndex - 1].Name : null),
                name.RefersTo is null ? string.Empty : read.RefersTo);
        }

        return snapshot;
    }

    /// <summary>
    /// The sheet's validations, each as a read reports it with the prompts a read leaves out,
    /// tracked across the operation (<see cref="Tracked"/>): a validation whose cells change is
    /// one change, and replacing one with the same rule over the same cells changes nothing,
    /// wherever the engine puts it in its list.
    /// </summary>
    private OperationSnapshot Validations(CellsSheetOp operation)
    {
        Worksheet sheet = Sheet(operation);
        var objects = new List<Anchored>();
        foreach (Validation validation in sheet.Validations)
        {
            if (ObjectProjection.Validation(sheet, validation) is { } read)
            {
                objects.Add(new Anchored("validation", validation, validation.Areas, CellsAddresses.At("validation", sheet.Name, read.Range),
                    Join(Read(read, Json.CellsValidationInfo), validation.InCellDropDown, validation.ShowInput, validation.ShowError)));
            }
        }

        return Tracked(operation, new OperationSnapshot(), objects, static area => area);
    }

    // Each rule by its cells as a read reports them, and its kind, formulas and style as the engine
    // keeps them: a read reports a rule in the operation's terms, which an engine rule may not have.
    private OperationSnapshot ConditionalFormats(CellsSheetOp operation)
    {
        Worksheet sheet = Sheet(operation);
        FormatCondition[] rules = Rules(sheet);
        return new OperationSnapshot().AddSequence(CellsAddresses.Sequence("conditional-format", sheet.Name), ObjectProjection.ConditionalFormats(sheet)
            .Select((read, index) => (CellsAddresses.Indexed("conditional-format", sheet.Name, index), Join(read.Range,
                rules[index].Type, rules[index].Operator, rules[index].Formula1, rules[index].Formula2, StyleKey(rules[index].Style)))));
    }

    /// <summary>The sheet's sparkline groups in order, each as a read reports it, with its kind, color and sparklines as the engine keeps them.</summary>
    private static OperationSnapshot AddSparklineGroups(OperationSnapshot snapshot, Worksheet sheet) =>
        snapshot.AddSequence(CellsAddresses.Sequence("sparkline-group", sheet.Name), Enumerable.Range(0, sheet.SparklineGroups.Count)
            .Select(index => (CellsAddresses.Indexed("sparkline-group", sheet.Name, index),
                Join(Read(ObjectProjection.Sparkline(sheet, sheet.SparklineGroups[index]), Json.CellsSparklineInfo),
                    sheet.SparklineGroups[index].Type, sheet.SparklineGroups[index].SeriesColor.Color.ToArgb(), Sparklines(sheet.SparklineGroups[index])))));

    private OperationSnapshot Charts(CellsSheetOp operation)
    {
        Worksheet sheet = Sheet(operation);
        return new OperationSnapshot().AddSequence(CellsAddresses.Sequence("chart", sheet.Name), Enumerable.Range(0, sheet.Charts.Count)
            .Select(index => (CellsAddresses.Indexed("chart", sheet.Name, index), ChartState(sheet, index))));
    }

    /// <summary>
    /// A chart as a read reports it, but for its place in the sheet's list, which its address names
    /// (a chart after a deleted one keeps its state), and its axis titles; and what the engine keeps
    /// that a read reports more coarsely or not at all: its identity and anchor, so that one deleted and another
    /// inserted, or one moved, counts; its titles' texts and legend position; and each series' data,
    /// colors and labels, and each pie slice's color, where a read reports one cover and one list.
    /// </summary>
    private static string ChartState(Worksheet sheet, int index)
    {
        Chart chart = sheet.Charts[index];
        bool pie = ChartPivotOps.IsPieFamily(chart.Type);
        bool scatter = chart.Type.ToString().StartsWith("Scatter", StringComparison.Ordinal);
        // Markers are read on a scatter only: reading them on a line chart gives it markers (probed on 26.9.0).
        IEnumerable<string> series = chart.NSeries.Cast<Series>().Select(item => Join(item.Name, item.Values, item.XValues,
            item.Area.ForegroundColor.ToArgb(), item.Border.Color.ToArgb(),
            scatter ? item.Marker.Area.ForegroundColor.ToArgb() : null, scatter ? item.Marker.Border.Color.ToArgb() : null,
            item.DataLabels.ShowValue, item.DataLabels.NumberFormat, item.DataLabels.NumberFormatLinked,
            pie ? string.Join(",", item.Points.Cast<ChartPoint>().Select(static point => point.Area.ForegroundColor.ToArgb())) : null));
        return Join(Identity(chart), Anchor(chart.ChartObject),
            Read(ObjectProjection.Chart(sheet, chart, index + 1) with { Index = 0, AxisTitles = null }, Json.CellsChartInfo),
            chart.Title?.Text, chart.Title?.IsAutoText, chart.Legend.Position, chart.CategoryAxis.Title?.Text, chart.ValueAxis.Title?.Text,
            string.Join(";", series));
    }

    // A pivot as a read reports it, but for its values and captions, with its source, grand total
    // caption and fields as the engine keeps them, and the values it shows, which a refresh changes
    // and a cell read reports.
    private OperationSnapshot Pivots(CellsSheetOp operation)
    {
        Worksheet sheet = Sheet(operation);
        var snapshot = new OperationSnapshot();
        foreach (PivotTable pivot in sheet.PivotTables)
        {
            CellsPivotInfo read = ObjectProjection.Pivot(sheet, pivot);
            CellArea area = pivot.TableRange1;
            string fields = string.Join(";", pivot.RowFields.Cast<PivotField>().Select(static field => "r:" + field.Name)
                .Concat(pivot.ColumnFields.Cast<PivotField>().Select(static field => "c:" + field.Name))
                .Concat(pivot.DataFields.Cast<PivotField>().Select(static field => "d:" + Join(field.Name, field.DisplayName, field.Function, field.NumberFormat))));
            IEnumerable<string> shown = Enumerable.Range(area.StartRow, area.EndRow - area.StartRow + 1)
                .SelectMany(row => Enumerable.Range(area.StartColumn, area.EndColumn - area.StartColumn + 1).Select(column => sheet.Cells.CheckCell(row, column)))
                .OfType<Cell>().Select(ComparisonValue.Stored);
            snapshot.Add(CellsAddresses.Pivot(read.Sheet, read.Name), Join(Read(read with { Values = null, Captions = string.Empty }, Json.CellsPivotInfo),
                string.Join(",", pivot.DataSource ?? []), pivot.GrandTotalName, fields, string.Join("|", shown)));
        }

        return snapshot;
    }

    private static OperationSnapshot One(string address, string state) => new OperationSnapshot().Add(address, state);

    /// <summary>An object as a read reports it, as text.</summary>
    private static string Read<T>(T? read, JsonTypeInfo<T> type) where T : class => JsonSerializer.Serialize(read!, type);

    private static string Identity(object item) =>
        Identities.GetValue(item, static _ => Interlocked.Increment(ref _nextIdentity).ToString(CultureInfo.InvariantCulture));

    private static string Join(params object?[] parts) =>
        string.Join("\u001F", parts.Select(static part => part switch
        {
            null => "-",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => part.ToString(),
        }));
}
