using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// Factory for the Cells-specific errors the CLI raises. Split out from
/// <see cref="CliErrors"/> so the neutral error surface stays free of
/// spreadsheet vocabulary; the same house rule applies — state the fact,
/// include the valid alternatives, recommend the fix.
/// </summary>
public static class CellsErrors
{
    internal static CliException TextExportEvaluationLimit(
        string format,
        string requestedSheet,
        string firstSheet) => CliErrors.EvaluationLimit(
        $"Evaluation mode can export only the first worksheet '{firstSheet}' to {format}; requested worksheet '{requestedSheet}' was not exported and no output was written.",
        "Aspose.Cells",
        "explicitly choose the first worksheet with --sheet",
        new JsonObject
        {
            ["format"] = format,
            ["requestedSheet"] = requestedSheet,
            ["firstSheet"] = firstSheet,
        });

    /// <summary>The evaluation engine refuses to open more files in this process.</summary>
    internal static CliException EvaluationOpenLimit(string path) => CliErrors.EvaluationLimit(
        $"Evaluation mode opens at most 100 workbooks per process, and this process has reached that limit; {path} was not opened.",
        "Aspose.Cells",
        "run the command again in a new process");

    internal static CliException RangeInvalid(string spec, string reason) => new(
        CellsDiagnostics.RangeInvalid,
        $"Invalid range '{spec}': {reason}",
        hint: "Use A1 notation such as 'B2:D10', a single cell such as 'C5', or a sheet-qualified range such as 'Sales!A1:C10'.",
        details: new JsonObject { ["range"] = spec, ["reason"] = reason });

    internal static CliException RangeTooLarge(
        long requestedCells,
        int maxCells,
        string suggestion) => new(
        CellsDiagnostics.RangeTooLarge,
        $"The request covers {requestedCells} cells which exceeds the limit of {maxCells}.",
        hint: suggestion,
        details: new JsonObject
        {
            ["requestedCells"] = requestedCells,
            ["maxCells"] = maxCells,
        });

    internal static CliException RenderEmpty(string sheetName) => new(
        CellsDiagnostics.RenderEmpty,
        $"Sheet '{sheetName}' has no content to render.",
        hint: "Choose a sheet that has data with --sheet; 'aspose-cli cells inspect' lists each sheet's used range.",
        details: new JsonObject { ["sheet"] = sheetName });

    internal static CliException RenderFailed(
        string sheetName,
        string engineMessage) => CliErrors.RenderFailed(
        $"Sheet '{sheetName}'",
        engineMessage,
        "An embedded chart or picture on this sheet defeats the renderer. Render another sheet, or deliver the sheet's data with 'aspose-cli cells query range' or 'aspose-cli cells convert'.",
        new JsonObject { ["sheet"] = sheetName });

    /// <summary>A delimited text input is not UTF-8 and names no encoding.</summary>
    internal static CliException TextEncodingInvalid(string path, long offset) => CliErrors.InputEncodingInvalid(
        path,
        offset,
        $"Import it with its encoding: aspose-cli cells convert \"{path}\" --to xlsx --encoding gb18030 "
            + "(Chinese Windows and ERP exports; use big5, shift_jis, windows-1252 or another name for other sources).");

    /// <summary>A delimited text input writes numbers in a form invariant parsing would change.</summary>
    internal static CliException TextNumbersAmbiguous(string path, string sample, int line) => CliErrors.NumberFormatAmbiguous(
        path,
        sample,
        line,
        $"Import it with its culture: aspose-cli cells convert \"{path}\" --to xlsx --culture de-DE "
            + "(or another culture that writes a decimal comma). The culture also reads its dates.");

    internal static CliException FileCorrupt(string path, string reason) => CliErrors.InputUnreadable(
        path,
        "spreadsheet",
        reason,
        "Verify the file opens in a spreadsheet application and is one of the supported input formats.");
}
