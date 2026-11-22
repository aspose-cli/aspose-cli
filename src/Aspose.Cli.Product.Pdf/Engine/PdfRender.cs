using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Renders selected PDF pages to images: <c>pdf render</c>.</summary>
internal static class PdfRender
{
    internal static PdfRenderResult Run(PdfSession session, PdfRenderRequest request)
    {
        string filePath = request.Input;
        LicenseState state = session.Outputs.License;
        using LoadedPdf loaded = session.Loader.Open(filePath, request.Password);
        IReadOnlyList<int> pages = RenderSelection.Resolve(request.Pages, loaded.Document.Pages.Count, ErrorCodes.PageNotFound, "page");
        IReadOnlyList<string> paths = request.Output.Parts(pages);
        string outputDirectory = request.Output.Directory;
        using OutputSet writer = session.Outputs.BeginSet([outputDirectory], "pdf-render");
        var staged = new List<(int Page, string Path)>();
        foreach ((int pageNumber, string path) in pages.Zip(paths))
        {
            writer.Stage(path, request.Output.Format, request.Output.Overwrite, stagedPath =>
            {
                using FileStream stream = File.Create(stagedPath);
                PdfRaster.RenderPage(
                    session.Budgets,
                    loaded.Document,
                    pageNumber,
                    request.Output.Format.Id,
                    request.Dpi,
                    stream);
            });
            staged.Add((pageNumber, path));
        }

        IReadOnlyList<PublishedFile> files = writer.Commit();
        return new PdfRenderResult
        {
            Input = FileObjects.Input(filePath, loaded.FormatId, loaded.Protection.Encrypted),
            Outputs = staged.Select((item, index) => new PdfPageOutput(FileObjects.Output(files[index], request.Output.Format, encrypted: false))
            {
                Page = item.Page,
            }).ToArray(),
            Dpi = request.Output.Format.Raster ? request.Dpi : null,
            License = EnvelopeParts.License(state),
        };
    }

}
