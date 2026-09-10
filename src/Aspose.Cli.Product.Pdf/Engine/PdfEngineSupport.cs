using System.Text;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Product.Pdf.Engine.Mapping;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO;
using Aspose.Pdf;
using Aspose.Pdf.Text;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Shared PDF document selection, text extraction, and output projection.</summary>
internal static class PdfEngineSupport
{
    internal static Document Select(Document source, IReadOnlyList<int> pages)
    {
        var selected = new Document();
        try
        {
            foreach (int pageNumber in pages)
            {
                selected.Pages.Add(source.Pages[pageNumber]);
            }

            return selected;
        }
        catch
        {
            selected.Dispose();
            throw;
        }
    }

    internal static string ExtractText(Page page, string mode)
    {
        TextExtractionOptions.TextFormattingMode formatting = mode == PdfReadModes.Layout
            ? TextExtractionOptions.TextFormattingMode.Pure
            : TextExtractionOptions.TextFormattingMode.Flatten;
        var absorber = new TextAbsorber
        {
            ExtractionOptions = new TextExtractionOptions(formatting),
        };
        page.Accept(absorber);
        return absorber.Text ?? string.Empty;
    }

    internal static bool IsImageDominated(Page page)
    {
        var absorber = new ImagePlacementAbsorber { IsReadOnlyMode = true };
        page.Accept(absorber);
        double pageArea = Math.Max(1d, page.Rect.Width * page.Rect.Height);
        return absorber.ImagePlacements.Any(placement =>
            placement.Rectangle.Width * placement.Rectangle.Height >= pageArea * 0.8d);
    }

    internal static string? NextCommand(
        string filePath,
        PdfReadRequest request,
        IReadOnlyList<int> requested,
        int consumed,
        bool truncated)
    {
        if (!truncated || consumed >= requested.Count)
        {
            return null;
        }

        string remaining = PageRangeText(requested.Skip(consumed));
        return $"aspose-cli pdf query pages \"{Path.GetFullPath(filePath)}\" --pages {remaining} --mode {request.Mode} --max-chars {request.MaxCharacters} --output json";
    }

    internal static string PageRangeText(IEnumerable<int> values) => string.Join(",", values);

    internal static string PagePath(string outputPath, int page)
    {
        string fullOutputPath = Path.GetFullPath(outputPath);
        string directory = Path.GetDirectoryName(fullOutputPath)!;
        string stem = Path.GetFileNameWithoutExtension(fullOutputPath);
        string extension = Path.GetExtension(fullOutputPath);
        return Path.Combine(directory, $"{stem}.p{page}{extension}");
    }

    internal static OutputInfo BuildOutput(string path, string format, long size) => new()
    {
        Path = Path.GetFullPath(path),
        Format = format,
        SizeBytes = size,
    };

    internal static PdfRect ToContractRect(Page page, Rectangle rect) => new()
    {
        X = rect.LLX,
        Y = page.Rect.Height - rect.URY,
        Width = rect.Width,
        Height = rect.Height,
    };
}
