using System.Globalization;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Views;
using Aspose.Words;
using Aspose.Words.Rendering;
using Aspose.Words.Saving;
using static Aspose.Cli.Product.Words.Engine.WordsEngineSupport;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>Serves <c>words render</c> and the page view of review and live display.</summary>
internal static class WordsRender
{
    /// <summary>Marks the page number in a multi-page output name: <c>report.p3.png</c>.</summary>
    private const string PagePart = "p";

    private const int CssDpi = 96;
    private const int EvidenceDpi = 150;
    private const string RenderHint = "Render fewer or smaller pages, or lower --dpi.";

    /// <summary>Renders selected pages within the pixel budget.</summary>
    internal static WordsRenderResult Run(WordsSession session, WordsRenderRequest request)
    {
        LicenseState state = session.Outputs.License;
        using LoadedDocument loaded = session.Loader.Open(request.Input, request.Password);
        IReadOnlyList<int> pages = request.AllPages
            ? Enumerable.Range(1, loaded.Document.PageCount).ToArray()
            : request.Pages?.Resolve(loaded.Document.PageCount) ?? [1];
        using OutputSet<Document> transaction = session.Outputs.BeginSet([request.Output.Directory], "words-render");
        var outputs = new List<PageOutput>(pages.Count);
        foreach (int page in pages)
        {
            PageInfo info = loaded.Document.GetPageInfo(page - 1);
            long width = (long)Math.Ceiling(info.WidthInPoints / 72d * request.Dpi);
            long height = (long)Math.Ceiling(info.HeightInPoints / 72d * request.Dpi);
            RenderPixelGuard.EnsureFits(session.Budgets, width, height, request.Dpi, RenderHint);
            string path = request.Output.Part(PagePart, page, pages.Count);
            SaveOptions options = WordsSavePipeline.Options(request.Output.Format.Id, pages: [page], dpi: request.Dpi);
            long size = transaction.Stage(path, request.Output.Overwrite, loaded.Document, temp => loaded.Document.Save(temp, options), rendering: true, pages: [page]).SizeBytes;
            outputs.Add(new PageOutput { Page = page, Output = BuildOutput(path, request.Output.Format.Id, size) });
        }

        loaded.Resources.ThrowIfFailed();
        transaction.Commit();
        return new WordsRenderResult
        {
            Input = InfoProjection.Source(request.Input, loaded),
            Outputs = outputs,
            Dpi = request.Output.Format.Id == "svg" ? null : request.Dpi,
            License = EnvelopeParts.License(state),
            Warnings = WrittenWarnings(loaded, request.Output.Format.Id, rendered: true),
        };
    }

    /// <summary>Renders the fixed-layout pages of one view, opening the document once.</summary>
    internal static ViewManifest View(
        WordsSession session,
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        _ = session.Outputs.License;
        using LoadedDocument loaded = session.Loader.Open(filePath, request.Password);
        Document document = loaded.Document;
        int dpi = request.Purpose == ViewPurpose.Display ? RenderPixelGuard.DefaultDpi : EvidenceDpi;
        int total = document.PageCount;
        int count = Math.Min(total, request.MaxPartCount);
        IReadOnlyList<IReadOnlyList<ViewElement>> layout = WordsViewLayout.Collect(document, loaded.Evaluation, count);
        var parts = new List<ViewPart>(count);
        for (int page = 1; page <= count; page++)
        {
            PageInfo info = document.GetPageInfo(page - 1);
            RenderPixelGuard.EnsureFits(
                session.Budgets,
                Pixels(info.WidthInPoints, dpi),
                Pixels(info.HeightInPoints, dpi),
                dpi,
                RenderHint);
            string file = string.Create(CultureInfo.InvariantCulture, $"page-{page:0000}.png");
            int pageNumber = page;
            artifacts.Write(
                file,
                stream => document.Save(
                    stream,
                    WordsSavePipeline.Options("png", pages: [pageNumber], dpi: dpi)));
            parts.Add(new ViewPart
            {
                Id = WordsViews.PagePart(page),
                Label = string.Create(CultureInfo.InvariantCulture, $"Page {page}"),
                File = file,
                Kind = ViewPartKinds.Image,
                Width = Pixels(info.WidthInPoints, CssDpi),
                Height = Pixels(info.HeightInPoints, CssDpi),
                Elements = layout[page - 1].Count == 0 ? null : layout[page - 1],
            });
        }

        loaded.Resources.ThrowIfFailed();
        return new ViewManifest
        {
            View = WordsViews.Pages,
            SourceFormat = loaded.FormatId,
            SourceSizeBytes = new FileInfo(filePath).Length,
            SourceEncrypted = loaded.Format.IsEncrypted,
            TotalPartCount = total,
            Parts = parts,
            Warnings = InputWarnings(loaded),
        };
    }

    private static int Pixels(double points, int dpi) =>
        checked((int)Math.Ceiling(points / 72d * dpi));
}
