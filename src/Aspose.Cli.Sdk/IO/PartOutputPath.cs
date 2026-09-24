using System.Globalization;

namespace Aspose.Cli.Sdk.IO;

/// <summary>
/// Names the files of a multi-part output: with the owning product's part marker <c>p</c>,
/// <c>report.png</c> becomes <c>report.p3.png</c> for part 3.
/// </summary>
public static class PartOutputPath
{
    /// <summary>Marker of a page-numbered part.</summary>
    public const string Page = "p";

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
