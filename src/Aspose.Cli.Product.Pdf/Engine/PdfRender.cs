using System.Globalization;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;

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
        IReadOnlyList<int> pages = request.AllPages
            ? Enumerable.Range(1, loaded.Document.Pages.Count).ToArray()
            : request.Pages?.Resolve(loaded.Document.Pages.Count) ?? [1];
        string outputDirectory = request.Output.Directory;
        using OutputSet<Document> writer = session.Outputs.BeginSet([outputDirectory], "pdf-render");
        var staged = new List<(int Page, string Path)>();
        foreach (int pageNumber in pages)
        {
            string path = request.Output.Part(PdfRaster.PagePart, pageNumber, pages.Count);
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

        IReadOnlyList<long> sizes = writer.Commit();
        return new PdfRenderResult
        {
            Input = PdfInfoProjection.Source(filePath),
            Outputs = staged.Select((item, index) => new PdfPageOutput
            {
                Page = item.Page,
                Output = BuildOutput(item.Path, request.Output.Format.Id, sizes[index]),
            }).ToArray(),
            Dpi = request.Output.Format.Id == "svg" ? null : request.Dpi,
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

        if (request.Output.Format.Id == "svg")
        {
            throw CliErrors.OptionInvalid(
                "--grid",
                "applies only to png and jpeg output",
                "Render the page with --to png or --to jpeg to draw a coordinate grid.");
        }

        if (spacing is < PdfGridOverlay.MinimumSpacing or > PdfGridOverlay.MaximumSpacing)
        {
            throw CliErrors.OptionInvalid(
                "--grid",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"must be between {PdfGridOverlay.MinimumSpacing} and {PdfGridOverlay.MaximumSpacing} points"),
                "Choose a spacing such as --grid 50.");
        }

        return PdfGridOverlay.Describe(spacing);
    }
}
