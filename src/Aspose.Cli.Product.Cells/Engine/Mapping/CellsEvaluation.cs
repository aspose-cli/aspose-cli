using System.Text.RegularExpressions;
using Aspose.Cells;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// The one place that recognizes the worksheet Aspose.Cells evaluation mode adds to every
/// workbook it saves: an "Evaluation Warning" sheet (or "Evaluation Warning (1)", ... when the
/// name is taken) that holds only the evaluation notice and becomes the active sheet. Commands
/// change their defaults for the sheet only in a workbook the unlicensed engine opened, so a
/// licensed workbook with a sheet of that name keeps its active sheet; review reports the sheet
/// in any mode (<see cref="IsWarningSheet"/>), because a licensed re-save keeps it.
/// </summary>
internal static partial class CellsEvaluation
{
    private const string Notice = "Evaluation Only. Created with Aspose.Cells";

    /// <summary>
    /// When the unlicensed engine opened a workbook whose active sheet is an evaluation warning
    /// sheet, activates the first other sheet in memory, preferring a visible one, so every
    /// command that defaults to the active sheet reads the workbook's content, and returns the
    /// warning such a command reports (<see cref="LoadedWorkbook.SkippedSheetWarning"/>). The file
    /// is not changed.
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
            Message = $"The active sheet '{skipped}' is the evaluation warning sheet Aspose.Cells evaluation mode added when it saved this workbook, so this command, which names no sheet, uses '{replacement.Name}' in its place.",
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

    /// <summary>
    /// Discloses the evaluation notice an unlicensed save writes into a data output as content,
    /// where a reader takes it for data: a last row of CSV and TSV, a closing Markdown heading,
    /// JSON records and warning sheets. Other formats carry it as a watermark, which
    /// <c>EVAL_MODE</c> discloses; null for them and for a licensed save.
    /// </summary>
    internal static Warning? DescribeAddedNotice(LicenseState licenseState, string formatId)
    {
        string? where = licenseState != LicenseState.Evaluation ? null : formatId switch
        {
            "csv" or "tsv" => $"as the last row of the {formatId} output, after the data",
            "md" => "as a heading after the table in the md output",
            "json" => "in the json output, as a {\"watermark\": ...} record after the records of each sheet that has data rows and as the entry of each 'Evaluation Warning' sheet",
            _ => null,
        };
        return where is null ? null : new Warning
        {
            Code = CellsDiagnostics.EvaluationNoticeAdded,
            Message = $"Evaluation mode wrote the notice '{Notice}...' {where}; it is not workbook data.",
            Hint = "Tell the user, and remove the notice before anything reads the output as data; a license avoids it.",
            Docs = "cells/troubleshooting",
        };
    }

    /// <summary>
    /// Discloses the visible evaluation warning sheets that earlier evaluation saves added to the
    /// workbook, which a whole-workbook pdf, xps, html or mhtml export carries as pages of the
    /// notice; null for any other export, a single-sheet export and a licensed engine.
    /// </summary>
    internal static Warning? DescribeExportedWarningSheets(Workbook workbook, string formatId, int? selectedSheet)
    {
        if (workbook.IsLicensed || selectedSheet is not null || formatId is not ("pdf" or "xps" or "html" or "mhtml"))
        {
            return null;
        }

        string[] sheets = workbook.Worksheets.Cast<Worksheet>()
            .Where(static sheet => sheet.IsVisible && IsWarningSheet(sheet))
            .Select(static sheet => sheet.Name).ToArray();
        return sheets.Length == 0 ? null : new Warning
        {
            Code = CellsDiagnostics.EvaluationNoticeAdded,
            Message = $"The {formatId} output includes the evaluation warning sheet(s) '{string.Join("', '", sheets)}' that earlier "
                + $"evaluation saves added to the input: extra pages (or html tabs) that hold only the notice '{Notice}...', not workbook content.",
            Hint = "Tell the user. Export the content sheets one at a time with --sheet, or produce the input and the export with a license.",
            Docs = "cells/troubleshooting",
            Location = sheets.Length == 1 ? sheets[0] : null,
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
