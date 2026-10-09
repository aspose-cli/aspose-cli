using System.Text.RegularExpressions;
using Aspose.Cells;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// The one place that recognizes the worksheet Aspose.Cells evaluation mode adds to every
/// workbook it saves: an "Evaluation Warning" sheet (or "Evaluation Warning (1)", ... when the
/// name is taken) that holds only the evaluation notice and becomes the active sheet. Commands
/// skip the sheet as their default, and review and the write pipeline
/// (<see cref="CellsEvaluationProfile"/>) report it in any mode (<see cref="IsWarningSheet"/>),
/// because licensed commands read and re-save workbooks that evaluation saves produced.
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
            Hint = "Pass --sheet (or an operation's \"sheet\") to choose any sheet; names come from inspect.",
            Docs = "cells/troubleshooting",
            Location = skipped.Name,
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

    /// <summary>Whether <paramref name="text"/> is the evaluation notice.</summary>
    internal static bool IsNotice(string? text) => text?.StartsWith(Notice, StringComparison.Ordinal) == true;

    /// <summary>
    /// The zero-based last row of <paramref name="sheet"/> when it holds only the evaluation
    /// notice, as the last row of an evaluation CSV or TSV export does; otherwise null.
    /// </summary>
    internal static int? NoticeRow(Worksheet sheet)
    {
        if (sheet.Cells.MaxDataRow < 0 || sheet.Cells.CheckRow(sheet.Cells.MaxDataRow) is not { } row)
        {
            return null;
        }

        Cell[] values = [.. row.Cast<Cell>().Where(static cell => cell.Type != CellValueType.IsNull)];
        return values is [{ IsFormula: false } cell] && IsNotice(cell.StringValue) ? row.Index : null;
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
}
