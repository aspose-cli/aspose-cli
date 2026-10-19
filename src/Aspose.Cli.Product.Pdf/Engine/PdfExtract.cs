using System.Text;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Text;
using Aspose.Pdf;
using Aspose.Pdf.Text;
using static Aspose.Cli.Product.Pdf.Engine.PdfPages;
using DrawingImageFormat = Aspose.Pdf.Drawing.ImageFormat;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// Extracts bounded images, attachments, text, tables or form data into a directory:
/// <c>pdf extract</c>.
/// </summary>
internal static class PdfExtract
{
    internal static PdfExtractResult Run(PdfSession session, PdfExtractRequest request)
    {
        string filePath = request.Input;
        LicenseState state = session.Outputs.License;
        using LoadedPdf loaded = session.Loader.Open(filePath, request.Password);
        IReadOnlyList<int> pages = request.Pages?.Resolve(loaded.Document.Pages.Count)
            ?? Enumerable.Range(1, loaded.Document.Pages.Count).ToArray();
        // An attachment is copied as the PDF stores it, so it carries nothing evaluation mode adds.
        using ExtractionGuard guard = session.Outputs.BeginExtraction(request.Output.Path, request.Output.Overwrite,
            request.What == PdfExtractKinds.Attachments ? ExtractionContent.EmbeddedFiles : ExtractionContent.Derived);
        IReadOnlyList<(PublishedFile File, int? Page)> files = request.What switch
        {
            PdfExtractKinds.Images => ExtractImages(loaded.Document, pages, guard, session.Budgets),
            PdfExtractKinds.Attachments => ExtractAttachments(loaded.Document, guard),
            PdfExtractKinds.Text => [(guard.StageAllBytes("document.txt", Encoding.UTF8.GetBytes(DocumentText(loaded.Document, pages))), null)],
            PdfExtractKinds.Tables => ExtractTables(loaded.Document, pages, guard, request.ByteOrderMark),
            PdfExtractKinds.Forms => [(PdfForms.Export(loaded.Document, request.FormFormat
                ?? throw new InvalidOperationException("Form data is extracted in a format the command chose."), guard), null)],
            _ => throw new InvalidOperationException("The extraction registry and handler are out of sync."),
        };
        guard.Commit();
        return new PdfExtractResult
        {
            Input = PdfInfoProjection.Source(filePath, loaded),
            What = request.What,
            Outputs = [.. files.Select(static item => new PdfPageOutput(FileObjects.Companion(item.File)) { Page = item.Page })],
            License = EnvelopeParts.License(state),
        };
    }

    private static IReadOnlyList<(PublishedFile File, int? Page)> ExtractImages(
        Document document,
        IReadOnlyList<int> pages,
        ExtractionGuard guard,
        ResourceBudgetLedger budgets)
    {
        var items = new List<(PublishedFile File, int? Page)>();
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
                items.Add((guard.StageAllBytes($"image-{++number:000}.png", bytes), pageNumber));
            }
        }

        return items;
    }

    private static IReadOnlyList<(PublishedFile File, int? Page)> ExtractAttachments(
        Document document,
        ExtractionGuard guard)
    {
        var items = new List<(PublishedFile File, int? Page)>();
        foreach (FileSpecification file in document.EmbeddedFiles.OrderBy(
                     static item => item.UnicodeName ?? item.Name,
                     StringComparer.Ordinal))
        {
            string name = file.UnicodeName ?? file.Name ?? "attachment.bin";
            long declaredLength = file.Contents.CanSeek
                ? file.Contents.Length
                : 0;
            PublishedFile written = guard.Stage(
                name,
                declaredLength,
                output =>
                {
                    // budget-allow: ExtractionGuard supplies a BudgetWriteStream
                    // bounded by the remaining extraction byte budget.
                    file.Contents.CopyTo(output);
                },
                flatten: true);
            items.Add((written, null));
        }

        return items;
    }

    private static IReadOnlyList<(PublishedFile File, int? Page)> ExtractTables(
        Document document,
        IReadOnlyList<int> pages,
        ExtractionGuard guard,
        bool byteOrderMark)
    {
        var items = new List<(PublishedFile File, int? Page)>();
        int number = 0;
        foreach (int pageNumber in pages)
        {
            Page page = document.Pages[pageNumber];
            var absorber = new TableAbsorber();
            absorber.Visit(page);
            foreach (AbsorbedTable table in absorber.TableList)
            {
                byte[] bytes = CsvText.Encode(
                    table.RowList.Select(static row => row.CellList.Select(static cell => string.Concat(
                        cell.TextFragments.Select(static fragment => fragment.Text)))),
                    byteOrderMark);
                items.Add((guard.StageAllBytes($"table-{++number:000}.csv", bytes), pageNumber));
            }
        }

        return items;
    }
}
