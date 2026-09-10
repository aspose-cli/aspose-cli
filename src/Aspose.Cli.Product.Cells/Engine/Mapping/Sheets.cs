using Aspose.Cells;
using Aspose.Cli.Product.Cells.Addressing;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Worksheet lookups shared across the engine: resolving an op's target sheet
/// (with the <c>SHEET_NOT_FOUND</c> error that lists the alternatives),
/// enumerating sheet names, and computing a sheet's used range — the one place
/// that knowledge lives.
/// </summary>
internal static class Sheets
{
    /// <summary>Resolves the sheet an op targets (its <c>sheet</c> field).</summary>
    public static Worksheet Resolve(Workbook workbook, Op op) => Resolve(workbook, op.Sheet);

    /// <summary>
    /// Resolves a sheet by name — the active sheet when the name is null — or
    /// throws <c>SHEET_NOT_FOUND</c> listing the available names.
    /// </summary>
    public static Worksheet Resolve(Workbook workbook, string? sheetName)
    {
        if (sheetName is null)
        {
            return workbook.Worksheets[workbook.Worksheets.ActiveSheetIndex];
        }

        Worksheet? sheet = workbook.Worksheets[sheetName];
        return sheet ?? throw CellsErrors.SheetNotFound(sheetName, Names(workbook));
    }

    /// <summary>The workbook's sheet names, in sheet order.</summary>
    public static string[] Names(Workbook workbook)
    {
        var names = new string[workbook.Worksheets.Count];
        for (int i = 0; i < names.Length; i++)
        {
            names[i] = workbook.Worksheets[i].Name;
        }

        return names;
    }

    /// <summary>
    /// Quotes a sheet name that needs it in a qualified reference (shared by
    /// the mappers that hand qualified range strings to the engine).
    /// </summary>
    public static string Qualify(string name) =>
        name.Contains(' ', StringComparison.Ordinal)
            ? "'" + name.Replace("'", "''", StringComparison.Ordinal) + "'"
            : name;

    /// <summary>
    /// The range covering all data on the sheet — <c>(0,0)</c> to the last data
    /// row/column — or null when the sheet is empty. The one place used-range
    /// bounds are computed.
    /// </summary>
    public static RangeRef? UsedRange(Worksheet sheet)
    {
        int maxDataRow = sheet.Cells.MaxDataRow;
        int maxDataColumn = sheet.Cells.MaxDataColumn;
        return maxDataRow >= 0 && maxDataColumn >= 0
            ? new RangeRef(new CellRef(0, 0), new CellRef(maxDataRow, maxDataColumn))
            : null;
    }
}
