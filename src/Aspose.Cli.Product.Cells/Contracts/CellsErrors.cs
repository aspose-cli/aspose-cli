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
        "explicitly choose the first worksheet with --sheets 1",
        new JsonObject
        {
            ["format"] = format,
            ["requested"] = requestedSheet,
            ["available"] = new JsonArray(firstSheet),
        });

    /// <summary>
    /// An A1 reference that does not parse. It is an operation field's mistake wherever the parser
    /// meets one; a command that parses an option with it reports <c>OPTION_INVALID</c> instead.
    /// </summary>
    internal static CliException RangeInvalid(string spec, string reason) => CliErrors.OperationValueInvalid(
        $"Invalid range '{spec}': {reason}",
        reason,
        "Use A1 notation such as 'B2:D10', a single cell such as 'C5', or a sheet-qualified range such as 'Sales!A1:C10'.");

    /// <summary>
    /// A sheet that draws nothing. The hint selects <paramref name="withData"/>, the first other
    /// sheet that holds data, by the position <c>--sheets</c> takes, or says that none does.
    /// </summary>
    internal static CliException RenderEmpty(string sheetName, (int Position, string Name)? withData) => new(
        CellsDiagnostics.RenderEmpty,
        $"Sheet '{sheetName}' has no content to render.",
        hint: withData is { } other
            ? $"Render a sheet that has data, such as --sheets {other.Position} for '{other.Name}'; 'aspose-cli cells inspect' lists each sheet's used range."
            : "No other sheet has data to render either: write some with 'aspose-cli cells edit' first; 'aspose-cli cells inspect' lists each sheet's used range.",
        details: new JsonObject { ["sheet"] = sheetName });

    internal static CliException RenderFailed(
        string sheetName,
        string engineMessage) => CliErrors.RenderFailed(
        $"Sheet '{sheetName}'",
        engineMessage,
        "An embedded chart or picture on this sheet defeats the renderer. Render another sheet, or deliver the sheet's data with 'aspose-cli cells read' or 'aspose-cli cells convert'.",
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
}
