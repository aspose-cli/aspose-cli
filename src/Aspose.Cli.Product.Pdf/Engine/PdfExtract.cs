using System.Text;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;
using Aspose.Pdf.Text;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;
using DrawingImageFormat = Aspose.Pdf.Drawing.ImageFormat;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// Extracts bounded images, attachments, text or tables into a directory, or exports form
/// data: <c>pdf extract</c>.
/// </summary>
internal static class PdfExtract
{
    internal static ResultEnvelope Run(PdfSession session, IPdfExtractRequest request) => request switch
    {
        PdfFormExportRequest forms => PdfForms.Export(session, forms),
        PdfExtractRequest assets => Assets(session, assets),
        _ => throw new ArgumentException($"'{request.GetType().Name}' is not a PDF extraction request.", nameof(request)),
    };

    /// <summary>Extracts images, attachments, text or tables into the requested directory.</summary>
    internal static PdfExtractResult Assets(PdfSession session, PdfExtractRequest request)
    {
        string filePath = request.Input;
        LicenseState state = session.Outputs.License;
        using LoadedPdf loaded = session.Loader.Open(filePath, request.Password);
        IReadOnlyList<int> pages = request.Pages?.Resolve(loaded.Document.Pages.Count)
            ?? Enumerable.Range(1, loaded.Document.Pages.Count).ToArray();
        using ExtractionGuard guard = session.Outputs.BeginExtraction(request.Output.Path, request.Output.Overwrite);
        IReadOnlyList<PdfExtractedItem> items = request.What switch
        {
            "images" => ExtractImages(loaded.Document, pages, guard, session.Budgets),
            "attachments" => ExtractAttachments(loaded.Document, guard),
            "text" => ExtractTextArtifact(loaded.Document, pages, guard),
            "tables" => ExtractTables(loaded.Document, pages, guard, request.ByteOrderMark),
            _ => throw new InvalidOperationException("The extraction registry and handler are out of sync."),
        };
        guard.Commit();
        return new PdfExtractResult
        {
            Input = PdfInfoProjection.Source(filePath),
            What = request.What,
            Items = items,
            License = EnvelopeParts.License(state),
        };
    }

    private static IReadOnlyList<PdfExtractedItem> ExtractImages(
        Document document,
        IReadOnlyList<int> pages,
        ExtractionGuard guard,
        ResourceBudgetLedger budgets)
    {
        var items = new List<PdfExtractedItem>();
        int number = 0;
        foreach (int pageNumber in pages)
        {
            var absorber = new ImagePlacementAbsorber { IsReadOnlyMode = true };
            document.Pages[pageNumber].Accept(absorber);
            foreach (ImagePlacement placement in absorber.ImagePlacements)
            {
                // The image's own dimensions bound the decode, so check them before decoding.
                // The SDK's PNG encoder resizes its target stream, which the budgeted
                // extraction stream does not allow, so the bounded image is encoded in memory
                // and then written through the extraction budget.
                XImage image = placement.Image;
                RenderPixelGuard.EnsureFits(budgets, image.Width, image.Height, dpi: null,
                    hint: $"The image on page {pageNumber} is too large to decode; extract the other pages with --pages.");

                using var encoded = new MemoryStream();
                placement.Save(encoded, DrawingImageFormat.Png);
                byte[] bytes = encoded.ToArray();
                items.Add(new PdfExtractedItem
                {
                    Path = guard.WriteAllBytes($"image-{++number:000}.png", bytes),
                    Kind = "image",
                    SizeBytes = bytes.LongLength,
                    Page = pageNumber,
                });
            }
        }

        return items;
    }

    private static IReadOnlyList<PdfExtractedItem> ExtractAttachments(
        Document document,
        ExtractionGuard guard)
    {
        var items = new List<PdfExtractedItem>();
        foreach (FileSpecification file in document.EmbeddedFiles.OrderBy(
                     static item => item.UnicodeName ?? item.Name,
                     StringComparer.Ordinal))
        {
            string name = file.UnicodeName ?? file.Name ?? "attachment.bin";
            long declaredLength = file.Contents.CanSeek
                ? file.Contents.Length
                : 0;
            long writtenLength = 0;
            string path = guard.Write(
                name,
                declaredLength,
                output =>
                {
                    // budget-allow: ExtractionGuard supplies a BudgetWriteStream
                    // bounded by the remaining extraction byte budget.
                    file.Contents.CopyTo(output);
                    writtenLength = output.Length;
                },
                flatten: true);
            items.Add(new PdfExtractedItem
            {
                Path = path,
                Kind = "attachment",
                SizeBytes = writtenLength,
                Name = name,
            });
        }

        return items;
    }

    private static IReadOnlyList<PdfExtractedItem> ExtractTextArtifact(
        Document document,
        IReadOnlyList<int> pages,
        ExtractionGuard guard)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(DocumentText(document, pages));
        string path = guard.WriteAllBytes("document.txt", bytes);
        return
        [
            new PdfExtractedItem
            {
                Path = path,
                Kind = "text",
                SizeBytes = bytes.LongLength,
            },
        ];
    }

    private static IReadOnlyList<PdfExtractedItem> ExtractTables(
        Document document,
        IReadOnlyList<int> pages,
        ExtractionGuard guard,
        bool byteOrderMark)
    {
        var items = new List<PdfExtractedItem>();
        int number = 0;
        foreach (int pageNumber in pages)
        {
            Page page = document.Pages[pageNumber];
            var absorber = new TableAbsorber();
            absorber.Visit(page);
            foreach (AbsorbedTable table in absorber.TableList)
            {
                var csv = new StringBuilder();
                foreach (AbsorbedRow row in table.RowList)
                {
                    csv.AppendLine(string.Join(
                        ",",
                        row.CellList.Select(static cell => Csv(string.Concat(
                            cell.TextFragments.Select(static fragment => fragment.Text))))));
                }

                byte[] bytes = [.. byteOrderMark ? Encoding.UTF8.Preamble : [], .. Encoding.UTF8.GetBytes(csv.ToString())];
                string path = guard.WriteAllBytes($"table-{++number:000}.csv", bytes);
                items.Add(new PdfExtractedItem
                {
                    Path = path,
                    Kind = "table",
                    SizeBytes = bytes.LongLength,
                    Page = pageNumber,
                    Rect = ToContractRect(page, table.Rectangle),
                    Confidence = 0.5,
                });
            }
        }

        return items;
    }

    private static string Csv(string value)
    {
        string normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        return normalized.IndexOfAny([',', '"', '\n']) >= 0
            ? "\"" + normalized.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : normalized;
    }
}
