using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.TestKit;

/// <summary>Outputs for engine calls, which the command template resolves in the product.</summary>
public static class ResolvedOutputs
{
    /// <summary>
    /// The output at <paramref name="path"/> in the format of <paramref name="formats"/> named by
    /// <paramref name="format"/>, or otherwise in the first one whose extension the path carries.
    /// </summary>
    public static ResolvedOutput At(
        IEnumerable<FormatDescriptor> formats, string path, string? format = null, bool overwrite = false, string? backup = null) =>
        new(
            formats.First(candidate => format is null
                ? candidate.Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)
                : candidate.Id == format),
            path,
            overwrite,
            inPlace: backup is not null,
            backup);
}
