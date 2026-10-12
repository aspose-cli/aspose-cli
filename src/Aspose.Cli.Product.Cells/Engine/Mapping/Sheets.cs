using System.Globalization;
using Aspose.Cells;
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
    public static Worksheet Resolve(Workbook workbook, CellsOp op) => Resolve(workbook, op.TargetSheet);

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
    /// The positions a <c>--sheets</c> selection resolves to; a position past the last sheet
    /// that a sheet has as its name, such as <c>2024</c>, is that sheet's name given where its
    /// position belongs (<see cref="SelectedByName"/>).
    /// </summary>
    /// <param name="workbook">The open workbook.</param>
    /// <param name="text">The value as given, or null.</param>
    /// <param name="range">Whether the command also takes <c>--range</c>.</param>
    /// <param name="resolve">Resolves the selection, failing with <c>SHEET_NOT_FOUND</c> past the last sheet.</param>
    public static IReadOnlyList<int> Selected(Workbook workbook, string? text, bool range, Func<IReadOnlyList<int>> resolve)
    {
        try
        {
            return resolve();
        }
        catch (CliException error) when (error.Code == CellsDiagnostics.SheetNotFound && text is not null
            && workbook.Worksheets.Cast<Worksheet>().Any(sheet => string.Equals(sheet.Name, text.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw SelectedByName(workbook, text, range);
        }
    }

    /// <summary>
    /// The error for a <c>--sheets</c> value that names a sheet: <c>--sheets</c> selects by
    /// position, so <c>details.available</c> lists every sheet as <c>&lt;position&gt;: &lt;name&gt;</c>
    /// (a sheet name holds no ':') and the hint gives the position of the sheet meant.
    /// </summary>
    /// <param name="workbook">The open workbook.</param>
    /// <param name="text">The value as given, one name even when it holds commas.</param>
    /// <param name="range">Whether the command also takes <c>--range</c>, which names one sheet.</param>
    public static CliException SelectedByName(Workbook workbook, string text, bool range)
    {
        string[] names = [.. workbook.Worksheets.Cast<Worksheet>().Select(static sheet => sheet.Name)];
        string[] listed = [.. names.Select(static (name, index) => string.Create(CultureInfo.InvariantCulture, $"{index + 1}: {name}"))];
        static string NameOf(string entry) => entry.IndexOf(": ", StringComparison.Ordinal) is var colon and >= 0 ? entry[(colon + 2)..] : entry;
        var mistake = Mistake.Of(text, listed, keyOf: NameOf);
        int exact = Array.FindIndex(names, name => string.Equals(name, text.Trim(), StringComparison.OrdinalIgnoreCase));
        int position = exact >= 0 ? exact + 1
            : mistake.Suggestions is [var first, ..] ? Array.IndexOf(listed, first) + 1
            : 1;
        string example = names.Length == 0 ? "Summary" : names[position - 1];
        return CliErrors.OptionInvalid(
            "--sheets",
            $"'{text}' names a sheet, but --sheets selects sheets by position",
            $"Use the position of the sheet from details.available, the number before its name: --sheets {position}"
                + (range ? $", or name one sheet with --range, such as --range {QuotedName(example)}!A1:H20." : "."),
            mistake);
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
