using System.Globalization;
using System.Text.RegularExpressions;
using Aspose.Cells;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// The one place that recognizes what Aspose.Cells evaluation mode leaves in a file: the
/// worksheet it adds to every workbook it saves, an "Evaluation Warning" sheet (or "Evaluation
/// Warning (1)", ... when the name is taken) that holds only the evaluation notice and becomes the
/// active sheet, which commands skip as their default in any mode; and the notice it writes as the
/// last row of a CSV or TSV export, which a load takes out (<see cref="TakeNoticeRows"/>). Review
/// and the write pipeline (<see cref="CellsEvaluationProfile"/>) report the sheet in any mode
/// (<see cref="IsWarningSheet"/>), because licensed commands read and re-save workbooks that
/// evaluation saves produced.
/// </summary>
internal static partial class CellsEvaluation
{
    /// <summary>The start of the notice evaluation mode writes into a workbook and its exports.</summary>
    internal const string Notice = "Evaluation Only. Created with Aspose.Cells";

    /// <summary>
    /// When the active sheet of a workbook is an evaluation warning sheet, activates the first
    /// other sheet in memory, preferring a visible one, so every command that defaults to the
    /// active sheet reads the workbook's content, and returns the skip, whose warning such a
    /// command reports (<see cref="LoadedWorkbook.SkippedSheetWarning"/>) and which a save of the
    /// whole workbook undoes (<see cref="LoadedWorkbook.RestoreActiveSheet"/>).
    /// </summary>
    internal static SkippedWarningSheet? SkipActiveWarningSheet(Workbook workbook)
    {
        WorksheetCollection sheets = workbook.Worksheets;
        if (sheets.Count == 0 || !IsWarningSheet(sheets[sheets.ActiveSheetIndex]))
        {
            return null;
        }

        Worksheet[] content = sheets.Cast<Worksheet>().Where(static sheet => !IsWarningSheet(sheet)).ToArray();
        if ((content.FirstOrDefault(static sheet => sheet.IsVisible) ?? content.FirstOrDefault()) is not { } replacement)
        {
            return null;
        }

        Worksheet skipped = sheets[sheets.ActiveSheetIndex];
        sheets.ActiveSheetIndex = replacement.Index;
        return new SkippedWarningSheet(skipped, new Warning(CellsDiagnostics.ActiveSheetSkipped, $"The active sheet '{skipped.Name}' is the evaluation warning sheet Aspose.Cells evaluation mode added when it saved this workbook, so this command, which names no sheet, uses '{replacement.Name}' in its place.")
        {
            Docs = "cells/troubleshooting",
            Location = CellsAddresses.OfSheet("sheet", skipped.Name),
        });
    }

    /// <summary>
    /// The evaluation warning sheet <see cref="SkipActiveWarningSheet"/> deactivated in memory
    /// and the warning a command that defaulted to the active sheet reports.
    /// </summary>
    internal sealed record SkippedWarningSheet(Worksheet Sheet, Warning Warning)
    {
        /// <summary>Activates the skipped sheet again, unless the workbook no longer has it.</summary>
        internal void Restore(Workbook workbook)
        {
            if (workbook.Worksheets.Cast<Worksheet>().Contains(Sheet))
            {
                workbook.Worksheets.ActiveSheetIndex = Sheet.Index;
            }
        }
    }

    /// <summary>Whether <paramref name="text"/> starts as the evaluation notice does, as the only text of an evaluation warning sheet (<see cref="IsWarningSheet"/>).</summary>
    internal static bool IsNotice(string? text) => text?.StartsWith(Notice, StringComparison.Ordinal) == true;

    /// <summary>
    /// The zero-based last row of <paramref name="sheet"/> when it is the notice row an evaluation
    /// CSV or TSV export writes after the data: column A holds that whole sentence and nothing
    /// else (<see cref="NoticeLine"/>), and no other cell of the row holds anything; otherwise
    /// null. A row that holds more than the sentence, or holds it in another column, is data,
    /// which a load must not take out (<see cref="TakeNoticeRows"/>).
    /// </summary>
    internal static int? NoticeRow(Worksheet sheet)
    {
        if (sheet.Cells.MaxDataRow < 0 || sheet.Cells.CheckRow(sheet.Cells.MaxDataRow) is not { } row)
        {
            return null;
        }

        Cell[] values = [.. row.Cast<Cell>().Where(static cell => cell.Type != CellValueType.IsNull && cell.StringValue.Length > 0)];
        return values is [{ IsFormula: false, Column: 0 } cell] && NoticeLine().IsMatch(cell.StringValue) ? row.Index : null;
    }

    /// <summary>
    /// Takes the notice rows an unlicensed CSV or TSV export wrote after the data out of
    /// <paramref name="sheet"/>, a delimited text input as loaded, so that no command reads them
    /// as data or writes them back: the last row while it is one (<see cref="NoticeRow"/>), since
    /// an evaluation edit of such a file once wrote the old notice back before its own. Returns
    /// their zero-based rows, first to last; none when the sheet does not end with one.
    /// </summary>
    internal static IReadOnlyList<int> TakeNoticeRows(Worksheet sheet)
    {
        var rows = new List<int>();
        while (NoticeRow(sheet) is int row)
        {
            sheet.Cells.DeleteRow(row);
            rows.Insert(0, row);
        }

        return rows;
    }

    /// <summary>The warning that the notice <paramref name="rows"/> (zero-based) of <paramref name="sheet"/> were taken out.</summary>
    internal static Warning NoticeTaken(string sheet, IReadOnlyList<int> rows)
    {
        string[] numbers = [.. rows.Select(static row => (row + 1).ToString(CultureInfo.InvariantCulture))];
        bool one = numbers.Length == 1;
        return new(CellsDiagnostics.TextTableLayout,
            $"{(one ? "Row" : "Rows")} {string.Join(", ", numbers)} of the input {(one ? "is" : "are")} not data: {(one ? "it is" : "each is")} the evaluation notice "
                + $"'{Notice}...' an Aspose.Cells export without a license wrote after the data, so the load took {(one ? "it" : "them")} out: "
                + $"commands do not read {(one ? "it" : "them")}, and a file written from this input does not hold {(one ? "it" : "them")}.")
        {
            Hint = "Tell the user the file was written without a license.",
            Docs = "cells/troubleshooting",
            Location = CellsAddresses.At("row", sheet, one ? numbers[0] : $"{numbers[0]}:{numbers[^1]}"),
        };
    }

    /// <summary>Whether <paramref name="sheet"/> is named like a warning sheet and holds only the notice.</summary>
    internal static bool IsWarningSheet(Worksheet sheet)
    {
        if (sheet.Type != SheetType.Worksheet || !WarningSheetName().IsMatch(sheet.Name))
        {
            return false;
        }

        bool notice = false;
        foreach (Cell cell in sheet.Cells)
        {
            if (cell.Type == CellValueType.IsNull)
            {
                continue;
            }
            if (notice || cell.IsFormula || !IsNotice(cell.StringValue))
            {
                return false;
            }
            notice = true;
        }
        return notice;
    }

    [GeneratedRegex(@"^Evaluation Warning( \(\d+\))?$", RegexOptions.CultureInvariant)]
    private static partial Regex WarningSheetName();

    // The whole notice an evaluation export writes as a row, as Aspose.Cells 26.9.0 writes it.
    [GeneratedRegex(@"^Evaluation Only\. Created with Aspose\.Cells for \.NET\. Copyright 2003 - \d{4} Aspose Pty Ltd\.$", RegexOptions.CultureInvariant)]
    private static partial Regex NoticeLine();
}
