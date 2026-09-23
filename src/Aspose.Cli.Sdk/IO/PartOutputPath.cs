using System.Globalization;

namespace Aspose.Cli.Sdk.IO;

/// <summary>
/// Names the files of a multi-part output: <c>report.png</c> becomes <c>report.p3.png</c>
/// for page 3 and <c>deck.png</c> becomes <c>deck.s3.png</c> for slide 3.
/// </summary>
public static class PartOutputPath
{
    /// <summary>Marker of a page-numbered part.</summary>
    public const string Page = "p";

    /// <summary>Marker of a slide-numbered part.</summary>
    public const string Slide = "s";

    /// <summary>Returns the absolute path of one numbered part beside the requested output.</summary>
    public static string For(string outputPath, string marker, int number)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        string full = Path.GetFullPath(outputPath);
        return Path.Combine(
            Path.GetDirectoryName(full)!,
            string.Create(
                CultureInfo.InvariantCulture,
                $"{Path.GetFileNameWithoutExtension(full)}.{marker}{number}{Path.GetExtension(full)}"));
    }
}
