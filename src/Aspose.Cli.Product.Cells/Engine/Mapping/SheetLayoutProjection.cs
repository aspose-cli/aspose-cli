using Aspose.Cells;
using Aspose.Cli.Sdk.IO.Budgets;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Reads back the sheet layout of <c>cells inspect --detail layout</c> in the terms of the
/// operations that set it, so an edit's frozen panes, outline groups, filter and page setup
/// can be confirmed without a rendering.
/// </summary>
internal static class SheetLayoutProjection
{
    public static IReadOnlyList<CellsSheetLayoutInfo> Build(ResourceBudgetLedger budgets, Workbook workbook) =>
        [.. workbook.Worksheets.Cast<Worksheet>().Select(sheet => Build(budgets, sheet))];

    private static CellsSheetLayoutInfo Build(ResourceBudgetLedger budgets, Worksheet sheet) => new()
    {
        Sheet = sheet.Name,
        SheetView = SheetView(sheet),
        TabColor = TabColor(sheet),
        FreezePanes = FreezePanes(sheet),
        RowHeights = NullIfEmpty(Runs(budgets, Sizes(sheet, rows: true),
            static (first, last, height) => new CellsRowHeightInfo { First = first + 1, Last = last + 1, Height = height })),
        ColumnWidths = NullIfEmpty(Runs(budgets, Sizes(sheet, rows: false),
            static (first, last, width) => new CellsColumnWidthInfo { First = A1.ColumnName(first), Last = A1.ColumnName(last), WidthChars = width })),
        RowGroups = NullIfEmpty(Groups(budgets, Levels(sheet, rows: true),
            static (first, last, level, collapsed) => new CellsRowGroupInfo
            {
                First = first + 1, Last = last + 1, Level = level, Collapsed = collapsed,
            })),
        ColumnGroups = NullIfEmpty(Groups(budgets, Levels(sheet, rows: false),
            static (first, last, level, collapsed) => new CellsColumnGroupInfo
            {
                First = A1.ColumnName(first), Last = A1.ColumnName(last), Level = level, Collapsed = collapsed,
            })),
        Autofilter = Autofilter(sheet),
        Protection = Protection(sheet),
        PrintArea = PrintArea(sheet.PageSetup),
        PageSetup = PageSetup(sheet.PageSetup),
    };

    internal static CellsSheetViewInfo SheetView(Worksheet sheet) => new()
    {
        Gridlines = sheet.IsGridlinesVisible,
        ZoomPercent = sheet.Zoom,
        Headings = sheet.IsRowColumnHeadersVisible,
    };

    internal static CellsTabColorInfo? TabColor(Worksheet sheet) =>
        StylePool.ToHex(sheet.TabColor) is { } tab ? new CellsTabColorInfo { Color = tab } : null;

    internal static CellsFreezePanesInfo? FreezePanes(Worksheet sheet) =>
        sheet.GetFreezedPanes(out int row, out int column, out _, out _)
            ? new CellsFreezePanesInfo { Cell = A1.FormatCell(new CellRef(row, column)) }
            : null;

    internal static CellsAutofilterInfo? Autofilter(Worksheet sheet) =>
        Reference(sheet.AutoFilter.Range) is { } filter ? new CellsAutofilterInfo { Range = filter } : null;

    internal static CellsSheetProtectionInfo? Protection(Worksheet sheet) =>
        sheet.IsProtected ? new CellsSheetProtectionInfo { Allow = ProtectionVocabulary.Allowed(sheet.Protection) } : null;

    internal static CellsPageSetupInfo PageSetup(PageSetup page)
    {
        bool fitsToPages = !page.IsPercentScale;
        return new CellsPageSetupInfo
        {
            Orientation = page.Orientation == PageOrientationType.Landscape
                ? CellsPageOrientations.Landscape
                : CellsPageOrientations.Portrait,
            PageSize = PageSetupVocabulary.PageSize(page),
            FitToWidth = fitsToPages ? page.FitToPagesWide : null,
            FitToHeight = fitsToPages ? page.FitToPagesTall : null,
            ScalePercent = fitsToPages ? null : page.Zoom,
            Margins = new CellsMargins
            {
                Top = PageSetupVocabulary.Points(page.TopMargin),
                Bottom = PageSetupVocabulary.Points(page.BottomMargin),
                Left = PageSetupVocabulary.Points(page.LeftMargin),
                Right = PageSetupVocabulary.Points(page.RightMargin),
            },
            HeaderMargin = PageSetupVocabulary.Points(page.HeaderMargin),
            FooterMargin = PageSetupVocabulary.Points(page.FooterMargin),
            HeaderCenter = Text(page.GetHeader(1)),
            HeaderLeft = Text(page.GetHeader(0)),
            HeaderRight = Text(page.GetHeader(2)),
            FooterCenter = Text(page.GetFooter(1)),
            FooterLeft = Text(page.GetFooter(0)),
            FooterRight = Text(page.GetFooter(2)),
        };
    }

    /// <summary>The print area and print titles, or null when the sheet has none of them.</summary>
    internal static CellsPrintAreaInfo? PrintArea(PageSetup page)
    {
        string? range = Reference(page.PrintArea);
        string? rows = Reference(page.PrintTitleRows);
        string? columns = Reference(page.PrintTitleColumns);
        return range is null && rows is null && columns is null
            ? null
            : new CellsPrintAreaInfo { Range = range, TitleRows = rows, TitleColumns = columns };
    }

    /// <summary>
    /// A row or column as the layout reads it: its size when set apart (a row's height given
    /// rather than fitted to its content, a column's width other than the sheet's standard width),
    /// its outline level and whether it is hidden.
    /// </summary>
    internal readonly record struct Line(double? Size, int Level, bool Hidden);

    /// <summary>The row or column at an index from 0; one the sheet keeps nothing for is the default.</summary>
    internal static Line At(Worksheet sheet, bool rows, int index) =>
        rows ? Of(sheet.Cells.CheckRow(index)) : Of(sheet, sheet.Cells.CheckColumn(index));

    private static Line Of(Row? row) =>
        row is null ? default : new(row.IsHeightMatched ? null : Math.Round(row.Height, 2), row.GroupLevel, row.IsHidden);

    private static Line Of(Worksheet sheet, Column? column) =>
        column is null ? default : new(
            Math.Round(column.Width, 2) is var width && width != Math.Round(sheet.Cells.StandardWidth, 2) ? width : null,
            column.GroupLevel, column.IsHidden);

    // The rows or columns the sheet keeps, by index.
    private static IEnumerable<(int Index, Line Line)> Lines(Worksheet sheet, bool rows) => rows
        ? sheet.Cells.Rows.Cast<Row>().Select(static row => (row.Index, Of(row)))
        : sheet.Cells.Columns.Cast<Column>().Select(column => (column.Index, Of(sheet, column)));

    private static IEnumerable<(int Index, double Size)> Sizes(Worksheet sheet, bool rows) =>
        Lines(sheet, rows).Where(static line => line.Line.Size is not null).Select(static line => (line.Index, line.Line.Size!.Value));

    // The outline level and hidden state of each row or column that has a level, by index.
    private static IEnumerable<(int Index, int Level, bool Hidden)> Levels(Worksheet sheet, bool rows) =>
        Lines(sheet, rows).Where(static line => line.Line.Level > 0).Select(static line => (line.Index, line.Line.Level, line.Line.Hidden));

    // A run is adjacent rows or columns of one size.
    private static List<T> Runs<T>(ResourceBudgetLedger budgets, IEnumerable<(int Index, double Size)> sizes, Func<int, int, double, T> create)
    {
        var entries = new List<(int Index, double Size)>();
        foreach ((int Index, double Size) entry in sizes)
        {
            if (entries.Count % 1024 == 0)
            {
                budgets.Deadline.ThrowIfExpired("sheet-layout");
            }

            entries.Add(entry);
        }

        entries.Sort(static (left, right) => left.Index.CompareTo(right.Index));
        var runs = new List<T>();
        for (int start = 0; start < entries.Count;)
        {
            int end = start;
            while (end + 1 < entries.Count && entries[end + 1].Index == entries[end].Index + 1 && entries[end + 1].Size == entries[start].Size)
            {
                end++;
            }

            runs.Add(create(entries[start].Index, entries[end].Index, entries[start].Size));
            start = end + 1;
        }

        return runs;
    }

    // A group at level n is a run of adjacent rows or columns whose level is n or deeper.
    private static List<T> Groups<T>(
        ResourceBudgetLedger budgets,
        IEnumerable<(int Index, int Level, bool Hidden)> levels,
        Func<int, int, int, bool, T> create)
    {
        var entries = new List<(int Index, int Level, bool Hidden)>();
        foreach ((int Index, int Level, bool Hidden) entry in levels)
        {
            if (entries.Count % 1024 == 0)
            {
                budgets.Deadline.ThrowIfExpired("sheet-layout");
            }

            entries.Add(entry);
        }

        entries.Sort(static (left, right) => left.Index.CompareTo(right.Index));
        var groups = new List<T>();
        int deepest = entries.Count == 0 ? 0 : entries.Max(static entry => entry.Level);
        for (int level = 1; level <= deepest; level++)
        {
            int start = -1;
            int previous = -1;
            bool hidden = true;
            foreach ((int index, int entryLevel, bool entryHidden) in entries.Where(entry => entry.Level >= level))
            {
                if (start >= 0 && index != previous + 1)
                {
                    groups.Add(create(start, previous, level, hidden));
                    start = -1;
                }

                if (start < 0)
                {
                    (start, hidden) = (index, true);
                }

                hidden &= entryHidden;
                previous = index;
            }

            if (start >= 0)
            {
                groups.Add(create(start, previous, level, hidden));
            }
        }

        return groups;
    }

    // Excel stores print and filter references in absolute form ($A$1:$H$50); the operations
    // take them without the dollar signs.
    private static string? Reference(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Replace("$", string.Empty, StringComparison.Ordinal);

    private static string? Text(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static IReadOnlyList<T>? NullIfEmpty<T>(List<T> items) => items.Count == 0 ? null : items;
}
