using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.IO.Publication;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Tables;
using Aspose.Words;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary><c>words render</c>: Document.Save of each page as an image.</summary>
internal static class WordsRender
{
    internal static WordsRenderResult Run(WordsSession session, WordsRenderRequest request)
    {
        LicenseState state = session.Outputs.License;
        var log = new EngineWarningLog();
        using LoadedDocument loaded = session.Loader.Open(request.Input, request.Password, log);
        Document document = loaded.Document;
        IReadOnlyList<int> pages = RenderSelection.Resolve(request.Pages, document.PageCount, ErrorCodes.PageNotFound, "page");
        IReadOnlyList<string> paths = request.Output.Parts(pages);
        log.During = "save";
        using OutputSet set = session.Outputs.BeginSet([request.Output.Directory], "words-render");
        var outputs = new List<WordsPageOutput>(pages.Count);
        foreach ((int page, string path) in pages.Zip(paths))
        {
            if (request.Output.Format.Raster)
            {
                Aspose.Words.Rendering.PageInfo info = document.GetPageInfo(page - 1);
                RenderPixelGuard.EnsureFits(session.Budgets,
                    (long)Math.Ceiling(info.WidthInPoints / 72d * request.Dpi), (long)Math.Ceiling(info.HeightInPoints / 72d * request.Dpi), request.Dpi);
            }

            StagedOutput staged = set.Stage(path, request.Output.Format, request.Output.Overwrite,
                temp => document.Save(temp, WordsEngineFormats.Options(request.Output.Format.Id, pages: [page], dpi: request.Dpi)));
            outputs.Add(new WordsPageOutput(FileObjects.Output(staged.Published, request.Output.Format, encrypted: false)) { Page = page });
        }

        loaded.Resources.ThrowIfFailed();
        set.Commit();
        return new WordsRenderResult
        {
            Input = FileObjects.Input(request.Input, loaded.FormatId, loaded.Protection.Encrypted),
            Outputs = outputs,
            Dpi = request.Output.Format.Raster ? request.Dpi : null,
            EngineWarnings = log.Warnings.Count == 0 ? null : log.Warnings,
            License = EnvelopeParts.License(state),
            Warnings = loaded.Warnings.Count == 0 ? null : loaded.Warnings,
        };
    }
}
