using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Pdf.Commands;

/// <summary>Table formatting several PDF commands share.</summary>
internal static class PdfText
{
    /// <summary>The written PDF of <c>pdf create</c> and <c>pdf merge</c>.</summary>
    internal static void Written(PdfWriteResult result, TableSurface surface) =>
        surface.Out.WriteLine(
            $"{result.Action}: {result.Output.Path} ({result.Output.Format}, {TableText.Bytes(result.Output.SizeBytes)})");

    /// <summary>A rectangle as its top-left corner and size, such as <c>72,202 20x20</c>.</summary>
    internal static string Rectangle(PdfRect rect) =>
        $"{TableText.Points(rect.X)},{TableText.Points(rect.Y)} {TableText.Points(rect.Width)}x{TableText.Points(rect.Height)}";
}
