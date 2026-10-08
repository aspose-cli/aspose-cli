using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Pdf;

/// <summary>Product-owned bounded signatures for generic PDF routing.</summary>
internal static class PdfFormatRecognition
{
    internal static IReadOnlyDictionary<string, FileFormatRecognition>
        Rules
    { get; } = new Dictionary<string, FileFormatRecognition>(
        StringComparer.Ordinal)
    {
        ["pdf"] = FileFormatRecognition.Match(
            FileProbePattern.PdfHeader(),
            "PDF header"),
    };
}
