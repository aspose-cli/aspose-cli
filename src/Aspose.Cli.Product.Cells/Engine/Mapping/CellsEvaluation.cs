using System.Text.RegularExpressions;
using Aspose.Cells;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// The one place that recognizes the worksheet Aspose.Cells evaluation mode adds to every
/// workbook it saves: an "Evaluation Warning" sheet (or "Evaluation Warning (1)", ... when the
/// name is taken) that holds only the evaluation notice and becomes the active sheet. The
/// sheet is recognized only in a workbook the unlicensed engine opened, so a licensed workbook
/// with a sheet of that name is never misread.
/// </summary>
internal static partial class CellsEvaluation
{
    private const string Notice = "Evaluation Only. Created with Aspose.Cells";

    /// <summary>
    /// When the unlicensed engine opened a workbook whose active sheet is an evaluation warning
    /// sheet, activates the first other sheet in memory, preferring a visible one, so every
    /// command that defaults to the active sheet reads the workbook's content, and returns the
    /// warning that says so. The file is not changed.
    /// </summary>
    internal static Warning? SkipActiveWarningSheet(Workbook workbook)
    {
        WorksheetCollection sheets = workbook.Worksheets;
        if (workbook.IsLicensed || sheets.Count == 0 || !IsWarningSheet(sheets[sheets.ActiveSheetIndex]))
        {
            return null;
        }

        Worksheet[] content = sheets.Cast<Worksheet>().Where(static sheet => !IsWarningSheet(sheet)).ToArray();
        if ((content.FirstOrDefault(static sheet => sheet.IsVisible) ?? content.FirstOrDefault()) is not { } replacement)
        {
            return null;
        }

        string skipped = sheets[sheets.ActiveSheetIndex].Name;
        sheets.ActiveSheetIndex = replacement.Index;
        return new Warning
        {
            Code = CellsDiagnostics.EvaluationSheetSkipped,
            Message = $"The active sheet '{skipped}' is the evaluation warning sheet Aspose.Cells evaluation mode added when it saved this workbook, so this command uses '{replacement.Name}' wherever it defaults to the active sheet.",
            Hint = "Pass --sheet (or an operation's \"sheet\") to choose any sheet; names come from inspect.",
            Docs = "cells/troubleshooting",
            Location = skipped,
        };
    }

    /// <summary>
    /// Discloses the evaluation warning sheet a save just added and activated, given the sheet
    /// count and active sheet before the save; null when the save added none.
    /// </summary>
    internal static Warning? DescribeAddedWarningSheet(Workbook workbook, int sheetsBefore, string activeBefore)
    {
        WorksheetCollection sheets = workbook.Worksheets;
        if (workbook.IsLicensed || sheets.Count <= sheetsBefore || sheets.ActiveSheetIndex < sheetsBefore
            || !IsWarningSheet(sheets[sheets.ActiveSheetIndex]))
        {
            return null;
        }

        string added = sheets[sheets.ActiveSheetIndex].Name;
        return new Warning
        {
            Code = CellsDiagnostics.EvaluationSheetAdded,
            Message = $"Evaluation mode added the worksheet '{added}' to the saved workbook and made it the active sheet in place of '{activeBefore}'.",
            Hint = "Keep the sheet: it discloses evaluation output, and a license avoids it. Later commands that default to the active sheet skip it and warn EVALUATION_SHEET_SKIPPED; --sheet chooses any sheet.",
            Docs = "cells/troubleshooting",
            Location = added,
        };
    }

    private static bool IsWarningSheet(Worksheet sheet)
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
            if (notice || cell.IsFormula || !cell.StringValue.StartsWith(Notice, StringComparison.Ordinal))
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
