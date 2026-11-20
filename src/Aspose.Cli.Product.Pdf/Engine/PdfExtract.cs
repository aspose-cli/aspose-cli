using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Extraction;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Text;
using Aspose.Pdf;
using Aspose.Pdf.Text;
using DrawingImageFormat = Aspose.Pdf.Drawing.ImageFormat;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// <c>pdf extract</c>: images through ImagePlacementAbsorber and ImagePlacement.Save, attachments
/// through FileSpecification.Contents, tables through TableAbsorber, and the form data through
/// the Form facade's ExportJson, ExportFdf or ExportXfdf, into a directory, all or none.
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
            request.What == PdfExtractKinds.Attachments ? ExtractionContent.EmbeddedFiles : ExtractionContent.Derived, request.ByteOrderMark);
        IReadOnlyList<PdfExtractOutput> outputs = request.What == PdfExtractKinds.Attachments
            ? ExtractAttachments(loaded.Document, guard)
            : [.. Extract(request, loaded, pages, guard, session.Budgets)
                .Select(static item => new PdfExtractOutput(FileObjects.Companion(item.File)) { Page = item.Page })];
        guard.Commit();
        return new PdfExtractResult
        {
            Input = FileObjects.Input(filePath, loaded.FormatId, loaded.Protection.Encrypted),
            What = request.What,
            Outputs = outputs,
            License = EnvelopeParts.License(state),
        };
    }

    // The files of every kind but attachments, each with the page it comes from.
    private static IReadOnlyList<(PublishedFile File, int? Page)> Extract(
        PdfExtractRequest request, LoadedPdf loaded, IReadOnlyList<int> pages, ExtractionGuard guard, ResourceBudgetLedger budgets) =>
        request.What switch
        {
            PdfExtractKinds.Images => ExtractImages(loaded.Document, pages, guard, budgets),
            PdfExtractKinds.Tables => ExtractTables(loaded.Document, pages, guard, request.ByteOrderMark),
            PdfExtractKinds.Forms => [(ExportForm(loaded.Document, request.FormFormat
                ?? throw new InvalidOperationException("Form data is extracted in a format the command chose."), guard), null)],
            _ => throw new InvalidOperationException("The extraction registry and handler are out of sync."),
        };

    private static IReadOnlyList<(PublishedFile File, int? Page)> ExtractImages(
        Document document,
        IReadOnlyList<int> pages,
        ExtractionGuard guard,
        ResourceBudgetLedger budgets)
    {
        var items = new List<(PublishedFile File, int? Page)>();
        int number = 0;
        foreach ((ImagePlacement placement, int pageNumber) in Pictures(document, pages))
        {
            // The image's own dimensions bound the decode, so check them before decoding.
            // The SDK's PNG encoder resizes its target stream, which the budgeted
            // extraction stream does not allow, so the bounded image is encoded in memory
            // and then written through the extraction budget.
            XImage image = placement.Image;
            RenderPixelGuard.EnsureFits(budgets, image.Width, image.Height, dpi: null, target: $"page/{pageNumber}");

            using var encoded = new MemoryStream();
            placement.Save(encoded, DrawingImageFormat.Png);
            byte[] bytes = encoded.ToArray();
            items.Add((guard.StageAllBytes($"image-{++number:000}.png", bytes), pageNumber));
        }

        return items;
    }

    /// <summary>
    /// The pictures <c>pdf extract --what images</c> writes, one file each: every placement of an
    /// image object on the pages, in page order, with its page. A picture drawn inline in a page
    /// is not an image object.
    /// </summary>
    internal static IEnumerable<(ImagePlacement Placement, int Page)> Pictures(Document document, IEnumerable<int> pages) =>
        pages.SelectMany(page =>
        {
            var absorber = new ImagePlacementAbsorber { IsReadOnlyMode = true };
            document.Pages[page].Accept(absorber);
            return absorber.ImagePlacements.Select(placement => (placement, page));
        });

    // Each attachment as attachment-0001 and on, in the engine's order; its own name, which the
    // document's author chose, is reported and never used as a path.
    private static IReadOnlyList<PdfExtractOutput> ExtractAttachments(
        Document document,
        ExtractionGuard guard)
    {
        var items = new List<PdfExtractOutput>();
        foreach (FileSpecification file in document.EmbeddedFiles)
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

    /// <summary>Exports the form data with the Form facade's call for the format, as one file of the extraction.</summary>
    private static PublishedFile ExportForm(Document document, Aspose.Cli.Sdk.Formats.FormatDescriptor format, ExtractionGuard guard)
    {
        Action<Aspose.Pdf.Facades.Form, Stream> export = PdfEngineFormats.Of(format.Id).Export
            ?? throw new InvalidOperationException($"'{format.Id}' is not a form data format.");
        using var data = new MemoryStream();
        export(new Aspose.Pdf.Facades.Form(document), data);
        return guard.StageAllBytes("form" + (format.OutputExtension ?? format.Extensions[0]), data.ToArray());
    }
}
