using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>Outputs for engine calls, which the command template resolves in the product.</summary>
internal static class TestOutput
{
    /// <summary>
    /// The output at <paramref name="path"/> in the format named by <paramref name="format"/>, or
    /// otherwise in the first declared format whose extension the path carries.
    /// </summary>
    public static ResolvedOutput At(string path, string? format = null, bool overwrite = false, string? backup = null) =>
        new(
            CellsFormats.Definitions.First(candidate => format is null
                ? candidate.Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)
                : candidate.Id == format),
            path,
            overwrite,
            inPlace: backup is not null,
            backup);
}
