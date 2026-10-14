using System.Globalization;
using Aspose.Cells;
using Aspose.Cli.Sdk.Addressing;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// The <c>&lt;unit&gt;/&lt;address&gt;</c> address of each object of a workbook, from the names a
/// read reports: the one place a Cells name is escaped (<see cref="TargetName.Escape"/>). A sheet
/// before a <c>!</c>, as in <c>cell/Sales!B2</c>, escapes its own <c>!</c> too, so the first
/// <c>!</c> ends the sheet.
/// </summary>
internal static class CellsAddresses
{
    private const char SheetEnd = '!';

    /// <summary>An object of a sheet at a cell or a range, such as <c>cell/Sales!B2</c> or <c>merge/Sales!A1:C1</c>.</summary>
    internal static string At(string unit, string sheet, string where) => $"{unit}/{TargetName.Escape(sheet, SheetEnd)}!{where}";

    /// <summary>A cell, such as <c>cell/Sales!B2</c>.</summary>
    internal static string Cell(string sheet, int row, int column) => At("cell", sheet, A1.FormatCell(new CellRef(row, column)));

    /// <summary>An object of a sheet by the cells it covers, such as <c>merge/Sales!A1:C1</c>.</summary>
    internal static string Area(string unit, string sheet, CellArea area) => Areas(unit, sheet, [area]);

    /// <summary>An object of a sheet by the areas it covers, such as <c>validation/Sales!B2:B9,D2:D9</c>.</summary>
    internal static string Areas(string unit, string sheet, IEnumerable<CellArea> areas) =>
        At(unit, sheet, string.Join(",", areas.Select(static area =>
            A1.FormatRange(new RangeRef(new CellRef(area.StartRow, area.StartColumn), new CellRef(area.EndRow, area.EndColumn))))));

    /// <summary>A row by its index from 0, such as <c>row/Sales!5</c>, or a column, such as <c>column/Sales!C</c>.</summary>
    internal static string Line(string sheet, bool rows, int index) => rows
        ? At("row", sheet, (index + 1).ToString(CultureInfo.InvariantCulture))
        : At("column", sheet, A1.ColumnName(index));

    /// <summary>The one object of its unit a sheet has, such as <c>autofilter/Sales</c>.</summary>
    internal static string OfSheet(string unit, string sheet) => $"{unit}/{TargetName.Escape(sheet)}";

    /// <summary>The objects of a unit a sheet keeps in order, as one sequence of a snapshot, such as <c>chart:Sales</c>.</summary>
    internal static string Sequence(string unit, string sheet) => $"{unit}:{TargetName.Escape(sheet)}";

    /// <summary>One of the objects of a unit a sheet keeps in order, by its index from 0, such as <c>chart/Sales/1</c>.</summary>
    internal static string Indexed(string unit, string sheet, int index) =>
        string.Create(CultureInfo.InvariantCulture, $"{unit}/{TargetName.Escape(sheet)}/{index + 1}");

    /// <summary>A table, such as <c>table/Sales</c>.</summary>
    internal static string Table(string name) => $"table/{TargetName.Escape(name)}";

    /// <summary>A pivot table of a sheet, such as <c>pivot/Sales/PivotTable1</c>.</summary>
    internal static string Pivot(string sheet, string name) => $"pivot/{TargetName.Escape(sheet)}/{TargetName.Escape(name)}";

    /// <summary>A defined name, such as <c>name/TaxRate</c>, or <c>name/Sales!TaxRate</c> for one a sheet scopes.</summary>
    internal static string Name(string name, string? sheet = null) =>
        sheet is null ? $"name/{TargetName.Escape(name)}" : At("name", sheet, TargetName.Escape(name));
}
