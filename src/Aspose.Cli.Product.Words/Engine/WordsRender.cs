using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.IO.Publication;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;
using Aspose.Words;
using Aspose.Words.Rendering;
using Aspose.Words.Saving;
using static Aspose.Cli.Product.Words.Engine.WordsEngineSupport;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>Serves <c>words render</c>.</summary>
internal static class WordsRender
{
    private const string RenderHint = "Render fewer or smaller pages, or lower --dpi.";

    /// <summary>Renders selected pages; a raster page stays within the pixel budget.</summary>
    internal static WordsRenderResult Run(WordsSession session, WordsRenderRequest request)
    {
        LicenseState state = session.Outputs.License;
        using LoadedDocument loaded = session.Loader.Open(request.Input, request.Password);
        IReadOnlyList<int> pages = RenderSelection.Resolve(request.Pages, loaded.Document.PageCount, ErrorCodes.PageNotFound, "page");
        IReadOnlyList<string> paths = request.Output.Parts(pages);
        using OutputSet<Document> transaction = session.Outputs.BeginSet([request.Output.Directory], "words-render");
        var outputs = new List<WordsPageOutput>(pages.Count);
        foreach ((int page, string path) in pages.Zip(paths))
        {
            if (request.Output.Format.Raster)
            {
                PageInfo info = loaded.Document.GetPageInfo(page - 1);
                long width = (long)Math.Ceiling(info.WidthInPoints / 72d * request.Dpi);
                long height = (long)Math.Ceiling(info.HeightInPoints / 72d * request.Dpi);
                RenderPixelGuard.EnsureFits(session.Budgets, width, height, request.Dpi, RenderHint);
            }

            SaveOptions options = WordsSavePipeline.Options(request.Output.Format.Id, pages: [page], dpi: request.Dpi);
            StagedOutput staged = transaction.Stage(path, request.Output.Format, request.Output.Overwrite, loaded.Document, temp => loaded.Document.Save(temp, options), rendering: true, pages: [page]);
            outputs.Add(new WordsPageOutput(FileObjects.Output(staged.Published, request.Output.Format, encrypted: false)) { Page = page });
        }

        loaded.Resources.ThrowIfFailed();
        transaction.Commit();
        return new WordsRenderResult
        {
            Input = InfoProjection.Source(request.Input, loaded),
            Outputs = outputs,
            Dpi = request.Output.Format.Raster ? request.Dpi : null,
            License = EnvelopeParts.License(state),
            Warnings = WrittenWarnings(loaded, request.Output.Format.Id, rendered: true),
        };
    }
}
