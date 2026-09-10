using System.Text.Json.Nodes;
using Aspose.Cli.Product.Cells;
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
    internal static CliException RangeInvalid(string spec, string reason) => new(
        CellsDiagnostics.RangeInvalid,
        $"Invalid range '{spec}': {reason}",
        hint: "Use A1 notation such as 'B2:D10', a single cell such as 'C5', or a sheet-qualified range such as 'Sales!A1:C10'.",
        details: new JsonObject { ["range"] = spec, ["reason"] = reason });

    internal static CliException RangeTooLarge(
        long requestedCells,
        int maxCells,
        string suggestion) => new(
        ErrorCodes.RangeTooLarge,
        $"The request covers {requestedCells} cells which exceeds the limit of {maxCells}.",
        hint: suggestion,
        details: new JsonObject
        {
            ["requestedCells"] = requestedCells,
            ["maxCells"] = maxCells,
        });

    internal static CliException RenderEmpty(string sheetName) => new(
        ErrorCodes.RenderEmpty,
        $"Sheet '{sheetName}' has no content to render.",
        hint: "Choose a sheet that has data with --sheet; 'aspose-cli cells inspect' lists each sheet's used range.",
        details: new JsonObject { ["sheet"] = sheetName });

    internal static CliException RenderFailed(
        string sheetName,
        string engineMessage) => new(
        ErrorCodes.RenderFailed,
        $"Sheet '{sheetName}' could not be rendered: the engine failed while rasterizing its content ({engineMessage}).",
        hint: "An embedded chart or picture on this sheet defeats the renderer. Render another sheet, or deliver the sheet's data with 'read' or 'convert'.",
        details: new JsonObject
        {
            ["sheet"] = sheetName,
            ["engineMessage"] = engineMessage,
        });

    public static CliException FileCorrupt(string path, string reason) => new(
        ErrorCodes.FileCorrupt,
        $"File could not be read as a spreadsheet: {path} ({reason})",
        hint: "Verify the file opens in a spreadsheet application and is one of the supported input formats.",
        details: new JsonObject { ["path"] = path, ["reason"] = reason });

    public static CliException SheetNotFound(string requested, IReadOnlyList<string> available)
    {
        var names = new JsonArray();
        foreach (string name in available)
        {
            names.Add(name);
        }

        return new CliException(
            CellsDiagnostics.SheetNotFound,
            $"Worksheet '{requested}' not found. Available sheets: {string.Join(", ", available)}",
            hint: "Use one of the available sheet names (they are case-sensitive), or run 'aspose-cli cells inspect <file>' to inspect the structure.",
            details: new JsonObject { ["requested"] = requested, ["available"] = names });
    }

    public static CliException OpsInvalid(string reason, string? hint = null) => new(
        ErrorCodes.OpsInvalid,
        $"The ops batch is invalid: {reason}",
        hint: hint ?? "Fix the ops document and retry; 'aspose-cli schema v2/cells/ops' documents every op.",
        details: new JsonObject { ["reason"] = reason });

    public static CliException OpsInvalidAt(int index, string opName, string reason, string? hint = null) => new(
        ErrorCodes.OpsInvalid,
        $"Op {index} ({opName}) is invalid: {reason}",
        hint: hint ?? "Fix this op and retry; nothing was written (batches are atomic).",
        details: new JsonObject { ["index"] = index, ["op"] = opName, ["reason"] = reason });

    /// <summary>Wraps a failure raised while applying one op of a batch.</summary>
    public static CliException OpsFailedAt(int index, string opName, CliException cause) => new(
        ErrorCodes.OpsInvalid,
        $"Op {index} ({opName}) failed: {cause.Message}",
        hint: cause.Hint ?? "Fix this op and retry; nothing was written (batches are atomic).",
        details: new JsonObject
        {
            ["index"] = index,
            ["op"] = opName,
            ["cause"] = cause.Code.Name,
        },
        innerException: cause);
}
