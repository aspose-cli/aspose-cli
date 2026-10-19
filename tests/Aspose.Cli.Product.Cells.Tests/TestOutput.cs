using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>Outputs for engine calls in the product's declared formats.</summary>
internal static class TestOutput
{
    /// <inheritdoc cref="ResolvedOutputs.At"/>
    public static ResolvedOutput At(string path, string? format = null, bool overwrite = false, string? backup = null) =>
        ResolvedOutputs.At(CellsFormats.Definitions, path, format, overwrite, backup);
}
