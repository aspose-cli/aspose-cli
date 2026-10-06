using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Contracts.Addressing;
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
    public static Worksheet Resolve(Workbook workbook, CellsOp op) => Resolve(workbook, op.Sheet);

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
        return sheet ?? throw CliErrors.NotFound(CellsDiagnostics.SheetNotFound, "sheet", sheetName, Names(workbook));
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
    /// Resolves a range that may be sheet-qualified: a qualified range names its own
    /// sheet (<c>SHEET_NOT_FOUND</c> when it does not exist), an unqualified one lies
    /// on <paramref name="opSheet"/>.
    /// </summary>
    public static (Worksheet Sheet, RangeRef Range) ResolveRange(Worksheet opSheet, string text)
    {
        RangeSpec spec = A1.ParseRange(text);
        Worksheet sheet = spec.SheetName is { } name ? Resolve(opSheet.Workbook, name) : opSheet;
        return (sheet, spec.Range);
    }

    /// <summary>
    /// The qualified reference the engine receives for a possibly qualified range,
    /// rebuilt from its parsed parts so the caller's spelling of the sheet never
    /// reaches the engine.
    /// </summary>
    public static string Reference(Worksheet opSheet, string text)
    {
        (Worksheet sheet, RangeRef range) = ResolveRange(opSheet, text);
        return Reference(sheet, range);
    }

    /// <summary>
    /// A qualified reference to <paramref name="range"/> on <paramref name="sheet"/>.
    /// The name is always quoted: digits, punctuation and spaces all require it, and
    /// a quoted plain name is equally valid.
    /// </summary>
    public static string Reference(Worksheet sheet, RangeRef range) =>
        QuotedName(sheet) + "!" + A1.FormatRange(range);

    /// <summary>Whether the workbook structure is protected against adding, deleting, renaming, moving and hiding sheets.</summary>
    public static bool StructureProtected(Workbook workbook) =>
        workbook.Settings.ProtectionType is ProtectionType.Structure or ProtectionType.All;

    /// <summary>Whether the workbook structure is protected with a password.</summary>
    public static bool StructurePasswordProtected(Workbook workbook) =>
        StructureProtected(workbook) && workbook.IsWorkbookProtectedWithPassword;

    /// <summary>The sheet's name quoted for a reference, embedded apostrophes doubled.</summary>
    public static string QuotedName(Worksheet sheet) => QuotedName(sheet.Name);

    /// <summary>A sheet name quoted for a reference, embedded apostrophes doubled.</summary>
    public static string QuotedName(string name) =>
        "'" + name.Replace("'", "''", StringComparison.Ordinal) + "'";

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
