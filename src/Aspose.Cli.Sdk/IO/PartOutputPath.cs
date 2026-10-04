using System.Globalization;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Sdk.IO;

/// <summary>
/// Names the files of an output written in parts: with the owning product's part marker <c>p</c>,
/// <c>report.png</c> becomes <c>report.p3.png</c> for part 3. An output without an extension,
/// often meant as a folder, is refused before any part is written, whatever the number of parts;
/// the hint names a file with the extension of the requested format.
/// </summary>
public static class PartOutputPath
{
    /// <summary>Marker of a page-numbered part.</summary>
    public const string Page = "p";

    /// <summary>
    /// Returns the absolute path of the part numbered <paramref name="number"/> of an output
    /// written in <paramref name="parts"/> parts: the requested output itself for a single part,
    /// and a numbered file beside it otherwise.
    /// </summary>
    public static string For(string outputPath, string marker, int number, int parts, string format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(parts);
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        string full = Path.GetFullPath(outputPath);
        if (!Path.HasExtension(full))
        {
            throw parts == 1
                ? CliErrors.OptionInvalid(
                    StandardOptionNames.Out,
                    "the output has no file extension",
                    $"Name a file with the {format} extension, such as {full}.{format}.")
                : CliErrors.OptionInvalid(
                    StandardOptionNames.Out,
                    "several parts are written and the output has no file extension",
                    $"Name a file with the {format} extension, such as {Path.Combine(full, $"page.{format}")}; each part is "
                        + $"written beside it with its number before the extension, as page.{marker}1.{format}, page.{marker}2.{format}.");
        }

        return parts == 1
            ? full
            : Path.Combine(
                Path.GetDirectoryName(full)!,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{Path.GetFileNameWithoutExtension(full)}.{marker}{number}{Path.GetExtension(full)}"));
    }
}
