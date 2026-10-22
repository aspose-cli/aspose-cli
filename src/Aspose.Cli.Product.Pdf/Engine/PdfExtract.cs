using System.Text;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Extraction;
using Aspose.Cli.Sdk.IO.Output;
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
        IReadOnlyList<PdfExtractOutput> outputs = request.What == PdfExtractKinds.Attachments
            ? ExtractAttachments(loaded.Document, guard)
            : [.. Extract(request, loaded, pages, guard, session.Budgets)
                .Select(static item => new PdfExtractOutput(FileObjects.Companion(item.File)) { Page = item.Page })];
        guard.Commit();
        return new PdfExtractResult
        {
            Input = PdfInfoProjection.Source(filePath, loaded),
            What = request.What,
            Outputs = outputs,
            License = EnvelopeParts.License(state),
            Warnings = request.What == PdfExtractKinds.Tables && outputs.Count == 0 ? [TablesNotFound()] : null,
        };
    }

    // The files of every kind but attachments, each with the page it comes from.
    private static IReadOnlyList<(PublishedFile File, int? Page)> Extract(
        PdfExtractRequest request, LoadedPdf loaded, IReadOnlyList<int> pages, ExtractionGuard guard, ResourceBudgetLedger budgets) =>
        request.What switch
        {
            PdfExtractKinds.Images => ExtractImages(loaded.Document, pages, guard, budgets),
            PdfExtractKinds.Text => [(guard.StageAllBytes("document.txt", Encoding.UTF8.GetBytes(DocumentText(loaded.Document, pages))), null)],
            PdfExtractKinds.Tables => ExtractTables(loaded.Document, pages, guard, request.ByteOrderMark),
            PdfExtractKinds.Forms => [(PdfForms.Export(loaded.Document, request.FormFormat
                ?? throw new InvalidOperationException("Form data is extracted in a format the command chose."), guard), null)],
            _ => throw new InvalidOperationException("The extraction registry and handler are out of sync."),
        };

    /// <summary>
    /// The table absorber recognizes a table by its ruling lines, so an empty result does not
    /// mean the pages hold no table.
    /// </summary>
    private static Warning TablesNotFound() => new(
        PdfDiagnostics.TablesNotFound,
        "No table was recognized on the selected pages; a table drawn without ruling lines, such as one laid out from Markdown, may not be recognized.")
    {
        Hint = "Compare with 'pdf extract --what text' or 'aspose-cli review' of the same pages, and take the table from the text if it is there.",
    };

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

    // Each attachment as attachment-0001 and on, in name order; its own name, which the
    // document's author chose, is reported and never used as a path.
    private static IReadOnlyList<PdfExtractOutput> ExtractAttachments(
        Document document,
        ExtractionGuard guard)
    {
        var items = new List<PdfExtractOutput>();
        foreach (FileSpecification file in document.EmbeddedFiles.OrderBy(
                     static item => item.UnicodeName ?? item.Name,
                     StringComparer.Ordinal))
        {
            string? name = file.UnicodeName ?? file.Name;
            long declaredLength = file.Contents.CanSeek
                ? file.Contents.Length
                : 0;
            PublishedFile written = guard.StageNamedByDocument(
                "attachment",
                name,
                declaredLength,
                output =>
                {
                    // budget-allow: ExtractionGuard supplies a BudgetWriteStream
                    // bounded by the remaining extraction byte budget.
                    file.Contents.CopyTo(output);
                });
            items.Add(new PdfExtractOutput(FileObjects.Companion(written)) { Name = string.IsNullOrEmpty(name) ? null : name });
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
