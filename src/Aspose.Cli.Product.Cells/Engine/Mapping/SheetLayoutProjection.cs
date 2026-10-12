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
            FreezePanes = sheet.GetFreezedPanes(out int row, out int column, out _, out _)
                ? new CellsFreezePanesInfo { Cell = A1.FormatCell(new CellRef(row, column)) }
                : null,
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
                    Header = Editing.PageSetupOps.Points(page.HeaderMargin),
                    Footer = Editing.PageSetupOps.Points(page.FooterMargin),
                },
                Header = Text(page.GetHeader(1)),
                Footer = Text(page.GetFooter(1)),
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
