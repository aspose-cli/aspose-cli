using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
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
/// operation (<see cref="IOperationModel{TOp}.Snapshot"/>), each at a <c>&lt;unit&gt;/&lt;address&gt;</c>
/// address:
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
/// A sheet is the one the operation names, resolved by name when the snapshot is taken.
/// </summary>
/// <param name="workbook">The workbook the batch edits.</param>
/// <param name="applied">Whether an operation was already applied: a snapshot after it, not before.</param>
internal sealed class CellsSnapshots(Workbook workbook, Func<CellsOp, bool> applied) : ICellsOpHandler<OperationSnapshot>
{
    // Most cells a range snapshot reads cell by cell; a larger range reads the cells the sheet holds.
    private const long DenseLimit = 100_000;

    // Identities of the objects the engine does not name, so a moved or renamed one is the same object.
    private static readonly ConditionalWeakTable<object, string> Identities = new();
    private static int _nextIdentity;

    // ----- cells ---------------------------------------------------------------------------------

    public OperationSnapshot Apply(SetValuesOp operation) => Cells(operation, Matrix(operation));

    public OperationSnapshot Apply(SetFormulaOp operation) => Cells(operation, operation.Range);

    public OperationSnapshot Apply(ClearRangeOp operation) => Cells(operation, operation.Range);

    public OperationSnapshot Apply(FormatRangeOp operation) => Cells(operation, operation.Range);

    public OperationSnapshot Apply(SetBordersOp operation) => Cells(operation, operation.Range);

    public OperationSnapshot Apply(SortRangeOp operation) => Cells(operation, operation.Range);

    public OperationSnapshot Apply(DeleteDuplicatesOp operation) => Cells(operation, operation.Range);

    public OperationSnapshot Apply(MergeCellsOp operation) => Merges(operation, operation.Range);

    public OperationSnapshot Apply(UnmergeCellsOp operation) => Merges(operation, operation.Range);

    public OperationSnapshot Apply(CopyRangeOp operation)
    {
        Worksheet sheet = Sheet(operation);
        (Worksheet fromSheet, RangeRef from) = Sheets.ResolveRange(sheet, operation.From);
        (Worksheet toSheet, RangeRef to) = Sheets.ResolveRange(sheet, operation.To);
        _ = fromSheet;
        var snapshot = new OperationSnapshot();
        AddCells(snapshot, toSheet, Landed(to.Start, from));
        AddMerges(snapshot, toSheet, Landed(to.Start, from));
        return snapshot;
    }

    public OperationSnapshot Apply(ImportRangeOp operation)
    {
        Worksheet sheet = Sheet(operation);
        RangeRef from = A1.ParseRange(operation.From).Range;
        (Worksheet toSheet, RangeRef to) = Sheets.ResolveRange(sheet, operation.To);
        var snapshot = new OperationSnapshot();
        AddCells(snapshot, toSheet, Landed(to.Start, from));
        AddMerges(snapshot, toSheet, Landed(to.Start, from));
        return snapshot;
    }

    public OperationSnapshot Apply(SetHyperlinkOp operation) => CellObjects(operation, operation.Cell, hyperlinks: true);

    public OperationSnapshot Apply(DeleteHyperlinkOp operation) => CellObjects(operation, operation.Cell, hyperlinks: true);

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

    public OperationSnapshot Apply(ResizeRowsOp operation) => Rows(operation, operation.First, operation.Last);

    public OperationSnapshot Apply(GroupRowsOp operation) => Rows(operation, operation.First, operation.Last);

    public OperationSnapshot Apply(UngroupRowsOp operation) => Rows(operation, operation.First, operation.Last);

    public OperationSnapshot Apply(ResizeColumnsOp operation) => Columns(operation, operation.First, operation.Last);

    public OperationSnapshot Apply(GroupColumnsOp operation) => Columns(operation, operation.First, operation.Last);

    public OperationSnapshot Apply(UngroupColumnsOp operation) => Columns(operation, operation.First, operation.Last);

    // ----- sheets ----------------------------------------------------------------------------------

    public OperationSnapshot Apply(InsertSheetOp operation) => SheetOrder();

    public OperationSnapshot Apply(ImportSheetOp operation) => SheetOrder();

    // The sheet keeps its place and identity: one change, at the new name.
    public OperationSnapshot Apply(RenameSheetOp operation) =>
        One(Address("sheet", operation.Name), RenamedSheet(operation)?.Name ?? "-");

    // Before the rename the sheet has its old name, or is the active one; after it, the new name.
    private Worksheet? RenamedSheet(RenameSheetOp operation) =>
        operation.Sheet is { } old && workbook.Worksheets[old] is { } found
            ? found
            : workbook.Worksheets[operation.Name] ?? (operation.Sheet is null ? workbook.Worksheets[workbook.Worksheets.ActiveSheetIndex] : null);

    public OperationSnapshot Apply(DeleteSheetOp operation) => SheetOrder();

    public OperationSnapshot Apply(MoveSheetOp operation) => SheetOrder();

    public OperationSnapshot Apply(SetSheetVisibilityOp operation) =>
        One(Address("sheet", Sheet(operation)), Sheet(operation).VisibilityType.ToString());

    public OperationSnapshot Apply(SetActiveSheetOp operation) =>
        One("workbook/active-sheet", ActiveSheet());

    public OperationSnapshot Apply(SetTabColorOp operation) => TabColor(operation);

    public OperationSnapshot Apply(ClearTabColorOp operation) => TabColor(operation);

    public OperationSnapshot Apply(SetSheetViewOp operation)
    {
        Worksheet sheet = Sheet(operation);
        return One(Address("sheet-view", sheet), Join(sheet.IsGridlinesVisible, sheet.Zoom, sheet.IsRowColumnHeadersVisible));
    }

    public OperationSnapshot Apply(FreezePanesOp operation) => FreezePanes(operation);

    public OperationSnapshot Apply(UnfreezePanesOp operation) => FreezePanes(operation);

    public OperationSnapshot Apply(SetAutoFilterOp operation) => AutoFilter(operation);

    public OperationSnapshot Apply(ClearAutoFilterOp operation) => AutoFilter(operation);

    public OperationSnapshot Apply(SetPrintAreaOp operation) => PrintArea(operation);

    public OperationSnapshot Apply(ClearPrintAreaOp operation) => PrintArea(operation);

    public OperationSnapshot Apply(SetPageSetupOp operation)
    {
        Worksheet sheet = Sheet(operation);
        PageSetup page = sheet.PageSetup;
        // Margins as a read reports them, in points: the engine keeps centimetres, so a value
        // written again in points may differ from the stored one in the last bits.
        return One(Address("page-setup", sheet), Join(
            page.Orientation, page.PaperSize, page.PaperWidth, page.PaperHeight, page.IsPercentScale, page.Zoom,
            page.FitToPagesWide, page.FitToPagesTall, PageSetupOps.Points(page.TopMargin), PageSetupOps.Points(page.BottomMargin),
            PageSetupOps.Points(page.LeftMargin), PageSetupOps.Points(page.RightMargin), PageSetupOps.Points(page.HeaderMargin),
            PageSetupOps.Points(page.FooterMargin),
            page.GetHeader(0), page.GetHeader(1), page.GetHeader(2), page.GetFooter(0), page.GetFooter(1), page.GetFooter(2)));
    }

    public OperationSnapshot Apply(ProtectSheetOp operation) => SheetProtection(operation);

    public OperationSnapshot Apply(UnprotectSheetOp operation) => SheetProtection(operation);

    public OperationSnapshot Apply(ProtectWorkbookOp operation) => WorkbookProtection();

    public OperationSnapshot Apply(UnprotectWorkbookOp operation) => WorkbookProtection();

    public OperationSnapshot Apply(SetDefaultFontOp operation)
    {
        Font font = workbook.DefaultStyle.Font;
        return One("workbook/default-font", Join(font.Name, font.DoubleSize));
    }

    // ----- names and objects ---------------------------------------------------------------------

    public OperationSnapshot Apply(DefineNameOp operation) => Names();

    public OperationSnapshot Apply(DeleteNameOp operation) => Names();

    public OperationSnapshot Apply(SetValidationOp operation) => Validations(operation);

    public OperationSnapshot Apply(ClearValidationOp operation) => Validations(operation);

    public OperationSnapshot Apply(AddConditionalFormatOp operation) => ConditionalFormats(operation);

    public OperationSnapshot Apply(ClearConditionalFormatsOp operation) => ConditionalFormats(operation);

    public OperationSnapshot Apply(AddSparklineOp operation) => Sparklines(operation);

    public OperationSnapshot Apply(DefineTableOp operation) => Tables(operation);

    public OperationSnapshot Apply(InsertChartOp operation) => Charts(operation);

    public OperationSnapshot Apply(SetChartOp operation) => Charts(operation);

    public OperationSnapshot Apply(DeleteChartOp operation) => Charts(operation);

    public OperationSnapshot Apply(InsertPivotOp operation) => Pivots(operation);

    public OperationSnapshot Apply(RefreshPivotOp operation) => Pivots(operation);

    public OperationSnapshot Apply(InsertImageOp operation)
    {
        Worksheet sheet = Sheet(operation);
        return new OperationSnapshot().AddSequence(Unit("image", sheet), Enumerable.Range(0, sheet.Pictures.Count)
            .Select(index =>
            {
                Picture picture = sheet.Pictures[index];
                return (Indexed("image", sheet, index), Join(
                    Identity(picture), Digest(picture.Data ?? []), picture.UpperLeftRow, picture.UpperLeftColumn, picture.Width, picture.Height));
            }));
    }

    // ----- addresses -------------------------------------------------------------------------------

    /// <summary>The address of a cell, such as <c>cell/Sales!B2</c>.</summary>
    internal static string CellAddress(Worksheet sheet, int row, int column) =>
        $"cell/{Named(sheet.Name)}!{A1.FormatCell(new CellRef(row, column))}";

    /// <summary>The address of a range of cells, such as <c>cell/Sales!B2:D10</c>, for an operation that did not apply.</summary>
    internal static string RangeAddress(string sheet, string range) => $"cell/{Named(sheet)}!{range}";

    /// <summary>
    /// A name as one segment of an address, with only the characters that would break the
    /// address escaped as %XX of their UTF-8 bytes: a slash, a percent sign, a control character,
    /// and white space at the start or end. Any other character, such as '!', an apostrophe, a
    /// CJK letter or an inner space, stays as it is; the last '!' of a cell address ends the sheet.
    /// </summary>
    internal static string Named(string name)
    {
        int first = 0;
        while (first < name.Length && char.IsWhiteSpace(name[first]))
        {
            first++;
        }

        int last = name.Length - 1;
        while (last >= first && char.IsWhiteSpace(name[last]))
        {
            last--;
        }

        var segment = new StringBuilder(name.Length);
        for (int index = 0; index < name.Length; index++)
        {
            char character = name[index];
            if (character is '/' or '%' || char.IsControl(character) || index < first || index > last)
            {
                foreach (byte value in Encoding.UTF8.GetBytes(character.ToString()))
                {
                    segment.Append(CultureInfo.InvariantCulture, $"%{value:X2}");
                }
            }
            else
            {
                segment.Append(character);
            }
        }

        return segment.ToString();
    }

    /// <summary>The address of an object of one sheet, such as <c>autofilter/Sales</c>.</summary>
    internal static string Address(string unit, Worksheet sheet) => Address(unit, sheet.Name);

    /// <summary>The address of an object of a named sheet, such as <c>autofilter/Sales</c>.</summary>
    internal static string Address(string unit, string sheet) => $"{unit}/{Named(sheet)}";

    private static string Unit(string unit, Worksheet sheet) => $"{unit}:{Named(sheet.Name)}";

    private static string Indexed(string unit, Worksheet sheet, int index) =>
        string.Create(CultureInfo.InvariantCulture, $"{unit}/{Named(sheet.Name)}/{index + 1}");

    // ----- helpers ---------------------------------------------------------------------------------

    private Worksheet Sheet(CellsSheetOp operation) => Sheets.Resolve(workbook, operation);

    private OperationSnapshot Cells(CellsSheetOp operation, string range)
    {
        Worksheet sheet = Sheet(operation);
        var snapshot = new OperationSnapshot();
        AddCells(snapshot, sheet, A1.ParseRange(range).Range);
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
            string state = CellState(cell);
            if (state.Length == 0 && StyleKey(cell.GetStyle()) == blank)
            {
                continue;
            }

            snapshot.Add(CellAddress(sheet, cell.Row, cell.Column), state + "|" + StyleKey(cell.GetStyle()));
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
                string text = A1.FormatRange(new RangeRef(new CellRef(area.StartRow, area.StartColumn), new CellRef(area.EndRow, area.EndColumn)));
                snapshot.Add($"merge/{Named(sheet.Name)}!{text}", "merged");
            }
        }
    }

    /// <summary>The stored value and formula of a cell; empty for an empty cell.</summary>
    internal static string CellState(Cell cell) => cell.IsFormula ? "f|" + cell.Formula : cell.Type switch
    {
        CellValueType.IsNull => cell.IsFormula ? "f|" + cell.Formula : string.Empty,
        CellValueType.IsNumeric or CellValueType.IsDateTime => "n|" + cell.DoubleValue.ToString("R", CultureInfo.InvariantCulture) + Formula(cell),
        CellValueType.IsBool => "b|" + (cell.BoolValue ? "1" : "0") + Formula(cell),
        CellValueType.IsError => "e|" + cell.StringValue + Formula(cell),
        _ => "s|" + cell.StringValue + Formula(cell),
    };

    private static string Formula(Cell cell) => cell.IsFormula ? "|f|" + cell.Formula : string.Empty;

    /// <summary>A text that differs exactly when two styles format a cell differently.</summary>
    internal static string StyleKey(Style style)
    {
        Font font = style.Font;
        var key = new StringBuilder();
        key.Append(font.Name).Append('|').Append(font.DoubleSize.ToString("R", CultureInfo.InvariantCulture))
            .Append('|').Append(font.IsBold).Append(font.IsItalic).Append(font.Underline).Append(font.IsStrikeout)
            .Append('|').Append(font.Color.ToArgb())
            .Append('|').Append(style.Pattern).Append(style.ForegroundColor.ToArgb()).Append(style.BackgroundColor.ToArgb())
            .Append('|').Append(style.Number).Append(style.Custom)
            .Append('|').Append(style.HorizontalAlignment).Append(style.VerticalAlignment).Append(style.IsTextWrapped)
            .Append(style.IndentLevel).Append(style.RotationAngle).Append(style.ShrinkToFit)
            .Append('|').Append(style.IsLocked).Append(style.IsFormulaHidden);
        foreach (BorderType edge in (BorderType[])[BorderType.TopBorder, BorderType.BottomBorder, BorderType.LeftBorder, BorderType.RightBorder,
                     BorderType.DiagonalDown, BorderType.DiagonalUp])
        {
            Border border = style.Borders[edge];
            key.Append('|').Append(border.LineStyle).Append(border.Color.ToArgb());
        }

        return key.ToString();
    }

    private OperationSnapshot CellObjects(CellsSheetOp operation, string cellText, bool hyperlinks)
    {
        Worksheet sheet = Sheet(operation);
        CellRef cell = A1.ParseCell(cellText);
        var snapshot = new OperationSnapshot();
        AddCells(snapshot, sheet, new RangeRef(cell, cell));
        if (hyperlinks)
        {
            AddKeyed(snapshot, sheet.Hyperlinks.Cast<Hyperlink>().Select(link =>
                (AreaAddress("hyperlink", sheet, link.Area), Join(link.Address, link.TextToDisplay, link.ScreenTip))));
        }

        return snapshot;
    }

    private OperationSnapshot Comments(CellsSheetOp operation)
    {
        Worksheet sheet = Sheet(operation);
        var snapshot = new OperationSnapshot();
        foreach (Comment comment in sheet.Comments)
        {
            snapshot.Add(
                $"comment/{Named(sheet.Name)}!{A1.FormatCell(new CellRef(comment.Row, comment.Column))}",
                Join(comment.Author, comment.Note));
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
    /// <remarks>
    /// Which object went where is known only once the engine shifted them, so the snapshot before
    /// the operation lists the objects as they are and the one after it completes both: it
    /// matches each object with itself, by the engine's own object where it keeps one (a comment,
    /// a link, a validation, a chart) and otherwise by where the shift should put it (a merged
    /// area), and adds what the operation changed. An object the engine left as it was is in
    /// neither, and an object removed and another moved into its place are two changes.
    /// </remarks>
    private OperationSnapshot Shift(CellsSheetOp operation, bool rows, int at, int count, bool insert)
    {
        Worksheet sheet = Sheet(operation);
        bool after = applied(operation);
        var snapshot = new OperationSnapshot();
        if (insert == after)
        {
            for (int line = at; line < at + count; line++)
            {
                snapshot.Add(LineAddress(sheet, rows, line), "line");
            }
        }

        IReadOnlyList<Anchored> objects = AnchoredObjects(sheet);
        if (!after)
        {
            _shifting = (operation, snapshot, objects);
            return snapshot;
        }

        if (_shifting is not { } shifting || !ReferenceEquals(shifting.Operation, operation))
        {
            throw new InvalidOperationException("The snapshot after a shift is taken after the snapshot before it.");
        }

        _shifting = null;
        string Place(Anchored item, bool predicted) => item.Areas is null ? item.Address : string.Join(",", item.Areas
            .Select(area => predicted ? Moved(area, rows, at, count, insert) : area)
            .OfType<CellArea>()
            .Select(static area => Join(area.StartRow, area.StartColumn, area.EndRow, area.EndColumn)));

        // Each object after the shift with itself before it: the engine's own object first, then
        // what lands where the shift should put it.
        var before = new List<Anchored>(shifting.Objects);
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

        snapshot.AddSequence("shift-shared", shared);
        shifting.Before.AddSequence("shift-removed", before.Select(static item => (item.Address, item.State)));
        return snapshot;
    }

    // The objects of the sheet before the shift that is being snapshot, and that snapshot.
    private (CellsOp Operation, OperationSnapshot Before, IReadOnlyList<Anchored> Objects)? _shifting;

    /// <summary>One object anchored on a sheet: its unit, the engine's own object where it keeps one, the areas it covers, its address and state.</summary>
    private sealed record Anchored(string Unit, object? Engine, IReadOnlyList<CellArea>? Areas, string Address, string State);

    /// <summary>Every object anchored on the sheet that inserting or deleting lines can move, resize or remove.</summary>
    private static List<Anchored> AnchoredObjects(Worksheet sheet)
    {
        static string Where(CellArea area) => Join(area.StartRow, area.StartColumn, area.EndRow, area.EndColumn);

        static CellArea Area(int startRow, int startColumn, int endRow, int endColumn) =>
            CellArea.CreateCellArea(startRow, startColumn, endRow, endColumn);

        var objects = new List<Anchored>();
        foreach (Comment comment in sheet.Comments)
        {
            CellArea cell = Area(comment.Row, comment.Column, comment.Row, comment.Column);
            objects.Add(new Anchored("comment", comment, [cell], AreaAddress("comment", sheet, cell), Join(Where(cell), comment.Author, comment.Note)));
        }

        foreach (Hyperlink link in sheet.Hyperlinks)
        {
            objects.Add(new Anchored("hyperlink", link, [link.Area], AreaAddress("hyperlink", sheet, link.Area),
                Join(Where(link.Area), link.Address, link.TextToDisplay)));
        }

        foreach (CellArea area in sheet.Cells.GetMergedAreas() ?? [])
        {
            objects.Add(new Anchored("merge", null, [area], AreaAddress("merge", sheet, area), Where(area)));
        }

        foreach (Validation validation in sheet.Validations)
        {
            // A validation whose cells were all deleted applies to nothing: it is gone.
            if (validation.Areas is { Length: > 0 } areas)
            {
                objects.Add(new Anchored("validation", validation, areas, ValidationAddress(sheet, areas), string.Join(";", areas.Select(Where))));
            }
        }

        for (int index = 0; index < sheet.Charts.Count; index++)
        {
            Chart chart = sheet.Charts[index];
            ChartShape shape = chart.ChartObject;
            objects.Add(new Anchored("chart", chart, null, Indexed("chart", sheet, index), Join(
                Where(Area(shape.UpperLeftRow, shape.UpperLeftColumn, shape.LowerRightRow, shape.LowerRightColumn)),
                string.Join(";", chart.NSeries.Cast<Series>().Select(static series => Join(series.Name, series.Values, series.XValues))))));
        }

        for (int index = 0; index < sheet.Pictures.Count; index++)
        {
            Picture picture = sheet.Pictures[index];
            objects.Add(new Anchored("image", picture, null, Indexed("image", sheet, index),
                Where(Area(picture.UpperLeftRow, picture.UpperLeftColumn, picture.LowerRightRow, picture.LowerRightColumn))));
        }

        int rule = 0;
        foreach (FormatConditionCollection conditions in sheet.ConditionalFormattings)
        {
            string areas = string.Join(";", Enumerable.Range(0, conditions.RangeCount).Select(i => Where(conditions.GetCellArea(i))));
            for (int condition = 0; condition < conditions.Count; condition++)
            {
                objects.Add(new Anchored("conditional-format", conditions[condition], null, Indexed("conditional-format", sheet, rule++), areas));
            }
        }

        for (int index = 0; index < sheet.SparklineGroups.Count; index++)
        {
            SparklineGroup group = sheet.SparklineGroups[index];
            objects.Add(new Anchored("sparkline-group", group, null, Indexed("sparkline-group", sheet, index), string.Join(";",
                group.Sparklines.Cast<Sparkline>().Select(static line => Join(line.DataRange, line.Row, line.Column)))));
        }

        foreach (ListObject table in sheet.ListObjects)
        {
            objects.Add(new Anchored("table", table, null, $"table/{Named(table.DisplayName)}",
                Where(Area(table.StartRow, table.StartColumn, table.EndRow, table.EndColumn))));
        }

        foreach (PivotTable pivot in sheet.PivotTables)
        {
            objects.Add(new Anchored("pivot", pivot, null, $"pivot/{Named(sheet.Name)}/{Named(pivot.Name)}", Where(pivot.TableRange1)));
        }

        return objects;
    }

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

    private static string LineAddress(Worksheet sheet, bool rows, int line) => rows
        ? string.Create(CultureInfo.InvariantCulture, $"row/{Named(sheet.Name)}!{line + 1}")
        : $"column/{Named(sheet.Name)}!{A1.ColumnName(line)}";

    /// <summary>The address of an object of a sheet by the cells it covers, such as <c>merge/Sales!A1:C1</c>.</summary>
    private static string AreaAddress(string unit, Worksheet sheet, CellArea area) =>
        $"{unit}/{Named(sheet.Name)}!{A1.FormatRange(new RangeRef(new CellRef(area.StartRow, area.StartColumn), new CellRef(area.EndRow, area.EndColumn)))}";

    /// <summary>A validation's address by the areas it applies to, such as <c>validation/Sales!B2:B9</c>.</summary>
    private static string ValidationAddress(Worksheet sheet, IEnumerable<CellArea> areas) =>
        $"validation/{Named(sheet.Name)}!" + string.Join(",", areas.Select(static area =>
            A1.FormatRange(new RangeRef(new CellRef(area.StartRow, area.StartColumn), new CellRef(area.EndRow, area.EndColumn)))));

    /// <summary>
    /// Adds objects addressed by what they cover; objects that share an address, such as two
    /// links over one cell, are one object whose state is theirs in order.
    /// </summary>
    private static void AddKeyed(OperationSnapshot snapshot, IEnumerable<(string Address, string State)> objects)
    {
        foreach (IGrouping<string, (string Address, string State)> same in objects.GroupBy(static item => item.Address, StringComparer.Ordinal))
        {
            snapshot.Add(same.Key, string.Join("\u001E", same.Select(static item => item.State)));
        }
    }

    private OperationSnapshot Rows(CellsSheetOp operation, int first, int? last)
    {
        Worksheet sheet = Sheet(operation);
        var snapshot = new OperationSnapshot();
        for (int row = first - 1; row <= (last ?? first) - 1; row++)
        {
            snapshot.Add(string.Create(CultureInfo.InvariantCulture, $"row/{Named(sheet.Name)}!{row + 1}"), RowFacts(sheet, row));
        }

        return snapshot;
    }

    private OperationSnapshot Columns(CellsSheetOp operation, string first, string? last)
    {
        Worksheet sheet = Sheet(operation);
        var snapshot = new OperationSnapshot();
        for (int column = A1.ParseColumn(first); column <= A1.ParseColumn(last ?? first); column++)
        {
            snapshot.Add($"column/{Named(sheet.Name)}!{A1.ColumnName(column)}", ColumnFacts(sheet, column));
        }

        return snapshot;
    }

    private static string RowFacts(Worksheet sheet, int row)
    {
        return Join(sheet.Cells.GetRowHeight(row), sheet.Cells.GetGroupedRowOutlineLevel(row), sheet.Cells.IsRowHidden(row));
    }

    private static string ColumnFacts(Worksheet sheet, int column)
    {
        return Join(sheet.Cells.GetColumnWidth(column), sheet.Cells.GetGroupedColumnOutlineLevel(column), sheet.Cells.IsColumnHidden(column));
    }

    /// <summary>The sheets in tab order, each by its identity and name, and the names that refer to them.</summary>
    private OperationSnapshot SheetOrder() =>
        new OperationSnapshot().AddSequence("sheet", workbook.Worksheets.Cast<Worksheet>()
            .Select(sheet => (Address("sheet", sheet), Join(Identity(sheet), sheet.Name))));

    private string ActiveSheet() =>
        workbook.Worksheets[workbook.Worksheets.ActiveSheetIndex].Name;

    private OperationSnapshot TabColor(CellsSheetOp operation)
    {
        Worksheet sheet = Sheet(operation);
        return One(Address("tab-color", sheet), sheet.TabColor.IsEmpty ? "-" : sheet.TabColor.ToArgb().ToString(CultureInfo.InvariantCulture));
    }

    private OperationSnapshot FreezePanes(CellsSheetOp operation)
    {
        Worksheet sheet = Sheet(operation);
        return One(Address("freeze-panes", sheet), sheet.GetFreezedPanes(out int row, out int column, out int rows, out int columns)
            ? Join(row, column, rows, columns)
            : "-");
    }

    private OperationSnapshot AutoFilter(CellsSheetOp operation)
    {
        Worksheet sheet = Sheet(operation);
        return One(Address("autofilter", sheet), sheet.AutoFilter.Range ?? "-");
    }

    private OperationSnapshot PrintArea(CellsSheetOp operation)
    {
        Worksheet sheet = Sheet(operation);
        PageSetup page = sheet.PageSetup;
        return One(Address("print-area", sheet), Join(page.PrintArea, page.PrintTitleRows, page.PrintTitleColumns));
    }

    private OperationSnapshot SheetProtection(CellsSheetOp operation)
    {
        Worksheet sheet = Sheet(operation);
        Protection protection = sheet.Protection;
        return One(Address("protection", sheet), Join(
            sheet.IsProtected, protection.IsProtectedWithPassword, protection.AllowFormattingCell, protection.AllowInsertingRow,
            protection.AllowInsertingColumn, protection.AllowDeletingRow, protection.AllowDeletingColumn, protection.AllowSorting,
            protection.AllowFiltering));
    }

    private OperationSnapshot WorkbookProtection() =>
        One("workbook/protection", Join(workbook.Settings.ProtectionType, workbook.IsWorkbookProtectedWithPassword));

    private OperationSnapshot Names()
    {
        var snapshot = new OperationSnapshot();
        foreach (Name name in workbook.Worksheets.Names)
        {
            string scope = name.SheetIndex > 0 ? Named(workbook.Worksheets[name.SheetIndex - 1].Name) + "!" : string.Empty;
            snapshot.Add($"name/{scope}{Named(name.Text)}", name.RefersTo ?? string.Empty);
        }

        return snapshot;
    }

    /// <summary>
    /// The sheet's validations by the areas each applies to: replacing one with the same rule over
    /// the same cells changes nothing, wherever the engine puts it in its list.
    /// </summary>
    private OperationSnapshot Validations(CellsSheetOp operation)
    {
        Worksheet sheet = Sheet(operation);
        var snapshot = new OperationSnapshot();
        // The rule as a read reports it: the engine stores a list given as items as ="a,b" and
        // reads it back as a,b, so the raw formula of the same list differs after a save.
        AddKeyed(snapshot, sheet.Validations.Cast<Validation>().Where(static validation => validation.Areas is { Length: > 0 }).Select(validation =>
        {
            CellsValidationInfo read = ObjectProjection.Validation(sheet, validation, validation.Areas.FirstOrDefault(), validation.Type.ToString());
            return (ValidationAddress(sheet, validation.Areas), Join(
                validation.Type, read.Operator, read.Value1, read.Value2, read.ListItems is { } items ? string.Join(",", items) : null,
                read.ListSource, read.AllowBlank, validation.InCellDropDown, validation.ShowInput, read.InputMessage, validation.ShowError, read.ErrorMessage));
        }));
        return snapshot;
    }

    private OperationSnapshot ConditionalFormats(CellsSheetOp operation)
    {
        Worksheet sheet = Sheet(operation);
        // One object per rule, in the order a read lists them: the areas of its collection and the rule.
        var rules = new List<string>();
        foreach (FormatConditionCollection conditions in sheet.ConditionalFormattings)
        {
            var areas = new StringBuilder();
            for (int area = 0; area < conditions.RangeCount; area++)
            {
                CellArea cells = conditions.GetCellArea(area);
                areas.Append(Join(cells.StartRow, cells.StartColumn, cells.EndRow, cells.EndColumn)).Append(';');
            }

            for (int condition = 0; condition < conditions.Count; condition++)
            {
                FormatCondition rule = conditions[condition];
                rules.Add(areas + Join(rule.Type, rule.Operator, rule.Formula1, rule.Formula2, StyleKey(rule.Style)));
            }
        }

        return new OperationSnapshot().AddSequence(Unit("conditional-format", sheet),
            rules.Select((state, index) => (Indexed("conditional-format", sheet, index), state)));
    }

    private OperationSnapshot Sparklines(CellsSheetOp operation)
    {
        Worksheet sheet = Sheet(operation);
        return new OperationSnapshot().AddSequence(Unit("sparkline-group", sheet), Enumerable.Range(0, sheet.SparklineGroups.Count)
            .Select(index =>
            {
                SparklineGroup group = sheet.SparklineGroups[index];
                string lines = string.Join(";", group.Sparklines.Cast<Sparkline>().Select(static line => Join(line.DataRange, line.Row, line.Column)));
                return (Indexed("sparkline-group", sheet, index), Join(group.Type, group.SeriesColor.Color.ToArgb(), lines));
            }));
    }

    private OperationSnapshot Tables(CellsSheetOp operation)
    {
        Worksheet sheet = Sheet(operation);
        var snapshot = new OperationSnapshot();
        foreach (ListObject table in sheet.ListObjects)
        {
            snapshot.Add($"table/{Named(table.DisplayName)}", Join(
                table.StartRow, table.StartColumn, table.EndRow, table.EndColumn, table.TableStyleType, table.TableStyleName, table.ShowTotals));
        }

        return snapshot;
    }

    private OperationSnapshot Charts(CellsSheetOp operation)
    {
        Worksheet sheet = Sheet(operation);
        return new OperationSnapshot().AddSequence(Unit("chart", sheet), Enumerable.Range(0, sheet.Charts.Count)
            .Select(index => (Indexed("chart", sheet, index), ChartState(sheet.Charts[index]))));
    }

    /// <summary>A text that differs exactly when a chart differs in what the chart operations set.</summary>
    internal static string ChartState(Chart chart)
    {
        var state = new StringBuilder();
        state.Append(Join(Identity(chart), chart.Type, chart.Name, chart.Title?.Text, chart.Title?.IsAutoText, chart.ShowLegend, chart.Legend.Position,
            chart.CategoryAxis.Title?.Text, chart.ValueAxis.Title?.Text,
            chart.ChartObject.UpperLeftRow, chart.ChartObject.UpperLeftColumn, chart.ChartObject.LowerRightRow, chart.ChartObject.LowerRightColumn));
        bool pie = ChartPivotOps.IsPieFamily(chart.Type);
        bool scatter = chart.Type.ToString().StartsWith("Scatter", StringComparison.Ordinal);
        foreach (Series series in chart.NSeries)
        {
            // Every place a series color is written and read back: the fill, the line, the
            // markers of a scatter and the slices of a pie. Markers are read on a scatter only:
            // reading them on a line chart gives it markers (probed on 26.9.0).
            state.Append(';').Append(Join(series.Name, series.Values, series.XValues, series.Area.ForegroundColor.ToArgb(),
                series.Border.Color.ToArgb(), scatter ? series.Marker.Area.ForegroundColor.ToArgb() : null,
                scatter ? series.Marker.Border.Color.ToArgb() : null,
                series.DataLabels.ShowValue, series.DataLabels.NumberFormat, series.DataLabels.NumberFormatLinked));
            if (pie)
            {
                foreach (ChartPoint point in series.Points)
                {
                    state.Append(',').Append(point.Area.ForegroundColor.ToArgb().ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        return state.ToString();
    }

    private OperationSnapshot Pivots(CellsSheetOp operation)
    {
        Worksheet sheet = Sheet(operation);
        var snapshot = new OperationSnapshot();
        foreach (PivotTable pivot in sheet.PivotTables)
        {
            CellArea area = pivot.TableRange1;
            var state = new StringBuilder(Join(pivot.Name, area.StartRow, area.StartColumn, area.EndRow, area.EndColumn,
                string.Join(",", pivot.DataSource ?? []), pivot.GrandTotalName));
            foreach (PivotField field in pivot.RowFields)
            {
                state.Append(";r:").Append(field.Name);
            }

            foreach (PivotField field in pivot.ColumnFields)
            {
                state.Append(";c:").Append(field.Name);
            }

            foreach (PivotField field in pivot.DataFields)
            {
                state.Append(";d:").Append(Join(field.Name, field.DisplayName, field.Function, field.NumberFormat));
            }

            // What a refresh changes: the values the pivot shows.
            for (int row = area.StartRow; row <= area.EndRow; row++)
            {
                for (int column = area.StartColumn; column <= area.EndColumn; column++)
                {
                    if (sheet.Cells.CheckCell(row, column) is { } cell)
                    {
                        state.Append('|').Append(CellState(cell));
                    }
                }
            }

            snapshot.Add($"pivot/{Named(sheet.Name)}/{Named(pivot.Name)}", state.ToString());
        }

        return snapshot;
    }

    private static OperationSnapshot One(string address, string state) => new OperationSnapshot().Add(address, state);

    private static string Identity(object item) =>
        Identities.GetValue(item, static _ => Interlocked.Increment(ref _nextIdentity).ToString(CultureInfo.InvariantCulture));

    private static string Digest(byte[] data) => Convert.ToHexString(SHA256.HashData(data));

    private static string Join(params object?[] parts) =>
        string.Join("\u001F", parts.Select(static part => part switch
        {
            null => "-",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => part.ToString(),
        }));
}
