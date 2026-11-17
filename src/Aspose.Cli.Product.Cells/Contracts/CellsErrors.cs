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
        reason);

    /// <summary>A sheet that draws nothing.</summary>
    internal static CliException RenderEmpty(string sheetName) => new(
        CellsDiagnostics.RenderEmpty,
        $"Sheet '{sheetName}' has no content to render.",
        details: new JsonObject { ["sheet"] = sheetName });

    internal static CliException RenderFailed(
        string sheetName,
        string engineMessage) => CliErrors.RenderFailed(
        $"Sheet '{sheetName}'",
        engineMessage,
        new JsonObject { ["sheet"] = sheetName });

    /// <summary>A delimited text input is not UTF-8 and names no encoding.</summary>
    internal static CliException TextEncodingInvalid(string path, long offset) => CliErrors.InputEncodingInvalid(
        path,
        offset);
}
