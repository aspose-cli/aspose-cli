using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>Frozen Cells error semantics owned by the product boundary.</summary>
internal static class CellsEngineErrors
{
    public static CliException RenderTooLarge(long width, long height, int dpi)
    {
        long megabytes = width * height * 4 / 1_048_576;
        var details = new JsonObject
        {
            ["width"] = width,
            ["height"] = height,
            ["dpi"] = dpi,
        };
        return new CliException(
            ErrorCodes.RenderTooLarge,
            $"Rendering this sheet at {dpi} dpi needs a {width}x{height} pixel image ({megabytes} MB), which cannot be allocated.",
            hint: "Render a window of the sheet with --range (e.g. --range A1:H50), or lower --dpi.",
            details: details);
    }
}
