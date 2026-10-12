using Aspose.Cells;
using Aspose.Cli.Sdk.IO;

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

    private static CellsSheetLayoutInfo Build(ResourceBudgetLedger budgets, Worksheet sheet)
    {
        PageSetup page = sheet.PageSetup;
        bool fitsToPages = !page.IsPercentScale;
        return new CellsSheetLayoutInfo
        {
            Sheet = sheet.Name,
            SheetView = new CellsSheetViewInfo
            {
                Gridlines = sheet.IsGridlinesVisible,
                ZoomPercent = sheet.Zoom,
                Headings = sheet.IsRowColumnHeadersVisible,
            },
            TabColor = StylePool.ToHex(sheet.TabColor) is { } tab ? new CellsTabColorInfo { Color = tab } : null,
            FreezePanes = sheet.GetFreezedPanes(out int row, out int column, out _, out _)
                ? new CellsFreezePanesInfo { Cell = A1.FormatCell(new CellRef(row, column)) }
                : null,
            RowHeights = NullIfEmpty(Runs(budgets, RowSizes(sheet),
                static (first, last, height) => new CellsRowHeightInfo { First = first + 1, Last = last + 1, Height = height })),
            ColumnWidths = NullIfEmpty(Runs(budgets, ColumnSizes(sheet),
                static (first, last, width) => new CellsColumnWidthInfo { First = A1.ColumnName(first), Last = A1.ColumnName(last), WidthChars = width })),
            RowGroups = NullIfEmpty(Groups(budgets, RowLevels(sheet),
                static (first, last, level, collapsed) => new CellsRowGroupInfo
                {
                    First = first + 1, Last = last + 1, Level = level, Collapsed = collapsed,
                })),
            ColumnGroups = NullIfEmpty(Groups(budgets, ColumnLevels(sheet),
                static (first, last, level, collapsed) => new CellsColumnGroupInfo
                {
                    First = A1.ColumnName(first), Last = A1.ColumnName(last), Level = level, Collapsed = collapsed,
                })),
            Autofilter = Reference(sheet.AutoFilter.Range) is { } filter ? new CellsAutofilterInfo { Range = filter } : null,
            Protection = sheet.IsProtected ? new CellsSheetProtectionInfo { Allow = Editing.ProtectOps.Allowed(sheet.Protection) } : null,
            PrintArea = PrintArea(page),
            PageSetup = new CellsPageSetupInfo
            {
                Orientation = page.Orientation == PageOrientationType.Landscape
                    ? CellsPageOrientations.Landscape
                    : CellsPageOrientations.Portrait,
                PageSize = Editing.PageSetupOps.PageSize(page),
                FitToWidth = fitsToPages ? page.FitToPagesWide : null,
                FitToHeight = fitsToPages ? page.FitToPagesTall : null,
                ScalePercent = fitsToPages ? null : page.Zoom,
                Margins = new CellsMargins
                {
                    Top = Editing.PageSetupOps.Points(page.TopMargin),
                    Bottom = Editing.PageSetupOps.Points(page.BottomMargin),
                    Left = Editing.PageSetupOps.Points(page.LeftMargin),
                    Right = Editing.PageSetupOps.Points(page.RightMargin),
                },
                HeaderMargin = Editing.PageSetupOps.Points(page.HeaderMargin),
                FooterMargin = Editing.PageSetupOps.Points(page.FooterMargin),
                HeaderCenter = Text(page.GetHeader(1)),
                HeaderLeft = Text(page.GetHeader(0)),
                HeaderRight = Text(page.GetHeader(2)),
                FooterCenter = Text(page.GetFooter(1)),
                FooterLeft = Text(page.GetFooter(0)),
                FooterRight = Text(page.GetFooter(2)),
            },
        };
    }

    /// <summary>The print area and print titles, or null when the sheet has none of them.</summary>
    private static CellsPrintAreaInfo? PrintArea(PageSetup page)
    {
        string? range = Reference(page.PrintArea);
        string? rows = Reference(page.PrintTitleRows);
        string? columns = Reference(page.PrintTitleColumns);
        return range is null && rows is null && columns is null
            ? null
            : new CellsPrintAreaInfo { Range = range, TitleRows = rows, TitleColumns = columns };
    }

    // The height of each row given one of its own rather than fitted to its content, by index.
    private static IEnumerable<(int Index, double Size)> RowSizes(Worksheet sheet)
    {
        foreach (Row row in sheet.Cells.Rows)
        {
            if (!row.IsHeightMatched)
            {
                yield return (row.Index, Math.Round(row.Height, 2));
            }
        }
    }

    // The width of each column other than the sheet's standard width, by index.
    private static IEnumerable<(int Index, double Size)> ColumnSizes(Worksheet sheet)
    {
        double standard = Math.Round(sheet.Cells.StandardWidth, 2);
        foreach (Column column in sheet.Cells.Columns)
        {
            double width = Math.Round(column.Width, 2);
            if (width != standard)
            {
                yield return (column.Index, width);
            }
        }
    }

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

    // The outline level and hidden state of each row or column that has a level, by index.
    private static IEnumerable<(int Index, int Level, bool Hidden)> RowLevels(Worksheet sheet)
    {
        foreach (Row row in sheet.Cells.Rows)
        {
            if (row.GroupLevel > 0)
            {
                yield return (row.Index, row.GroupLevel, row.IsHidden);
            }
        }
    }

    private static IEnumerable<(int Index, int Level, bool Hidden)> ColumnLevels(Worksheet sheet)
    {
        foreach (Column column in sheet.Cells.Columns)
        {
            if (column.GroupLevel > 0)
            {
                yield return (column.Index, column.GroupLevel, column.IsHidden);
            }
        }
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
