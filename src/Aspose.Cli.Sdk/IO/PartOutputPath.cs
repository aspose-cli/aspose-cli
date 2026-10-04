using System.Globalization;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Sdk.IO;

/// <summary>
/// Names the files of a multi-part output: with the owning product's part marker <c>p</c>,
/// <c>report.png</c> becomes <c>report.p3.png</c> for part 3.
/// </summary>
public static class PartOutputPath
{
    /// <summary>Marker of a page-numbered part.</summary>
    public const string Page = "p";

    /// <summary>
    /// Returns the absolute path of one numbered part beside the requested output. An output
    /// without an extension, often meant as a folder, is refused before any part is written; the
    /// hint names a file with the extension of the requested <paramref name="format"/>.
    /// </summary>
    public static string For(string outputPath, string marker, int number, string format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        string full = Path.GetFullPath(outputPath);
        if (!Path.HasExtension(full))
        {
            throw CliErrors.OptionInvalid(
                StandardOptionNames.Out,
                "several parts are written and the output has no file extension",
                $"Name a file with the {format} extension, such as {Path.Combine(full, $"page.{format}")}; each part is "
                    + $"written beside it with its number before the extension, as page.{marker}1.{format}, page.{marker}2.{format}.");
        }
        return Path.Combine(
            Path.GetDirectoryName(full)!,
            string.Create(
                CultureInfo.InvariantCulture,
                $"{Path.GetFileNameWithoutExtension(full)}.{marker}{number}{Path.GetExtension(full)}"));
    }
}
