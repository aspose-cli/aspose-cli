using System.Globalization;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Renders selected PDF pages to images: <c>pdf render</c>.</summary>
internal static class PdfRender
{
    internal static PdfRenderResult Run(PdfSession session, PdfRenderRequest request)
    {
        string filePath = request.Input;
        PdfRenderGrid? grid = RenderGrid(request);
        LicenseState state = session.Outputs.License;
        using LoadedPdf loaded = session.Loader.Open(filePath, request.Password);
        IReadOnlyList<int> pages = RenderSelection.Resolve(request.Pages, loaded.Document.Pages.Count, ErrorCodes.PageNotFound, "page");
        IReadOnlyList<string> paths = request.Output.Parts(pages);
        string outputDirectory = request.Output.Directory;
        using OutputSet<Document> writer = session.Outputs.BeginSet([outputDirectory], "pdf-render");
        var staged = new List<(int Page, string Path)>();
        foreach ((int pageNumber, string path) in pages.Zip(paths))
        {
            writer.Stage(path, request.Output.Overwrite, loaded.Document, stagedPath =>
            {
                using FileStream stream = File.Create(stagedPath);
                PdfRaster.RenderPage(
                    session.Budgets,
                    loaded.Document,
                    pageNumber,
                    request.Output.Format.Id,
                    request.Dpi,
                    stream,
                    grid);
            }, rendering: true, pages: [pageNumber]);
            staged.Add((pageNumber, path));
        }

        IReadOnlyList<PublishedFile> files = writer.Commit();
        return new PdfRenderResult
        {
            Input = PdfInfoProjection.Source(filePath, loaded),
            Outputs = staged.Select((item, index) => new PdfPageOutput(FileObjects.Output(files[index], request.Output.Format, encrypted: false))
            {
                Page = item.Page,
            }).ToArray(),
            Dpi = PdfEngineFormats.IsRaster(request.Output.Format.Id) ? request.Dpi : null,
            Grid = grid,
            License = EnvelopeParts.License(state),
        };
    }

    /// <summary>Validates the requested grid, which only raster output carries.</summary>
    private static PdfRenderGrid? RenderGrid(PdfRenderRequest request)
    {
        if (request.Grid is not int spacing)
        {
            return null;
        }

        if (!PdfEngineFormats.IsRaster(request.Output.Format.Id))
        {
            throw CliErrors.OptionInvalid(
                "--grid",
                "applies only to png and jpeg output",
                "Render the page with --to png or --to jpeg to draw a coordinate grid.");
        }

        if (spacing is < PdfRenderRequest.MinimumGrid or > PdfRenderRequest.MaximumGrid)
        {
            throw CliErrors.OptionInvalid(
                "--grid",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"must be between {PdfRenderRequest.MinimumGrid} and {PdfRenderRequest.MaximumGrid} points"),
                "Choose a spacing such as --grid 50.");
        }

        return PdfGridOverlay.Describe(spacing);
    }
}
