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
    public static IReadOnlyList<SheetLayoutInfo> Build(ResourceBudgetLedger budgets, Workbook workbook) =>
        [.. workbook.Worksheets.Cast<Worksheet>().Select(sheet => Build(budgets, sheet))];

    private static SheetLayoutInfo Build(ResourceBudgetLedger budgets, Worksheet sheet)
    {
        PageSetup page = sheet.PageSetup;
        bool fitsToPages = !page.IsPercentScale;
        return new SheetLayoutInfo
        {
            Sheet = sheet.Name,
            FreezePanes = sheet.GetFreezedPanes(out int row, out int column, out _, out _)
                ? A1.FormatCell(new CellRef(row, column))
                : null,
            RowGroups = NullIfEmpty(Groups(budgets, RowLevels(sheet),
                static (first, last, level, collapsed) => new RowGroupInfo
                {
                    From = first + 1, To = last + 1, Level = level, Collapsed = collapsed,
                })),
            ColumnGroups = NullIfEmpty(Groups(budgets, ColumnLevels(sheet),
                static (first, last, level, collapsed) => new ColumnGroupInfo
                {
                    From = A1.ColumnName(first), To = A1.ColumnName(last), Level = level, Collapsed = collapsed,
                })),
            AutoFilter = Reference(sheet.AutoFilter.Range),
            PrintArea = Reference(page.PrintArea),
            TitleRows = Reference(page.PrintTitleRows),
            TitleColumns = Reference(page.PrintTitleColumns),
            Orientation = page.Orientation == PageOrientationType.Landscape
                ? PageOrientations.Landscape
                : PageOrientations.Portrait,
            FitToWidth = fitsToPages ? page.FitToPagesWide : null,
            FitToHeight = fitsToPages ? page.FitToPagesTall : null,
            Scale = fitsToPages ? null : page.Zoom,
            Header = Text(page.GetHeader(1)),
            Footer = Text(page.GetFooter(1)),
        };
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
