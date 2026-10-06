using System.Text.RegularExpressions;
using Aspose.Cells;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// The one place that recognizes the worksheet Aspose.Cells evaluation mode adds to every
/// workbook it saves: an "Evaluation Warning" sheet (or "Evaluation Warning (1)", ... when the
/// name is taken) that holds only the evaluation notice and becomes the active sheet. Commands
/// skip the sheet as their default and review reports it in any mode
/// (<see cref="IsWarningSheet"/>), because licensed commands read and re-save workbooks that
/// evaluation saves produced.
/// </summary>
internal static partial class CellsEvaluation
{
    private const string Notice = "Evaluation Only. Created with Aspose.Cells";

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
        return new SkippedWarningSheet(skipped, new Warning
        {
            Code = CellsDiagnostics.EvaluationSheetSkipped,
            Message = $"The active sheet '{skipped.Name}' is the evaluation warning sheet Aspose.Cells evaluation mode added when it saved this workbook, so this command, which names no sheet, uses '{replacement.Name}' in its place.",
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
    /// notice in any mode; null for any other export and a single-sheet export.
    /// </summary>
    internal static Warning? DescribeExportedWarningSheets(Workbook workbook, string formatId, int? selectedSheet)
    {
        if (selectedSheet is not null || formatId is not ("pdf" or "xps" or "html" or "mhtml"))
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
