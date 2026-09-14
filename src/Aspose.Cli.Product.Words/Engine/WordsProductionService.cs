using System.Globalization;
using System.Text;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Preview;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;
using Aspose.Words;
using Aspose.Words.Rendering;
using Aspose.Words.Saving;
using static Aspose.Cli.Product.Words.Engine.WordsEngineSupport;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>Owns document conversion, rendering, preview and creation.</summary>
internal sealed class WordsProductionService
{
    private const int PreviewDpi = 144;
    private const int CssDpi = 96;
    private readonly ILicenseGate _licenseGate;
    private readonly SafeFileWriter _writer;
    private readonly WordsDocumentLoader _loader;
    private readonly InputSource _inputs;

    internal WordsProductionService(
        ILicenseGate licenseGate,
        SafeFileWriter writer,
        WordsDocumentLoader loader,
        InputSource inputs)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _inputs = inputs ?? throw new ArgumentNullException(nameof(inputs));
    }

    /// <summary>Converts a document using the selected save pipeline.</summary>
    internal WordsConvertResult Convert(string filePath, WordsConvertRequest request) =>
        WordsErrorTranslator.Execute("convert", () => ConvertCore(filePath, request));

    /// <summary>Renders selected pages within the pixel budget.</summary>
    internal WordsRenderResult Render(string filePath, WordsRenderRequest request) =>
        WordsErrorTranslator.Execute("render", () => RenderCore(filePath, request));

    /// <summary>Renders the embedded fixed-layout preview.</summary>
    internal PreviewRenderOutcome RenderPreview(
        string filePath,
        WordsPreviewRequest request,
        IPreviewArtifactSink artifacts) =>
        WordsErrorTranslator.Execute(
            "preview",
            () => RenderPreviewCore(filePath, request, artifacts));

    /// <summary>Creates a document from a bounded source or blank template.</summary>
    internal WordsCreateResult CreateDocument(NewDocumentRequest request) =>
        WordsErrorTranslator.Execute("create", () => CreateDocumentCore(request));

    private WordsConvertResult ConvertCore(string filePath, WordsConvertRequest request)
    {
        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedDocument loaded = _loader.Open(filePath, request.Password);
        IReadOnlyList<int>? pages = request.Pages?.Resolve(loaded.Document.PageCount);
        SaveOptions options = WordsSavePipeline.Options(request.TargetFormatId, request.EncryptPassword, pages);
        long size = _writer.Write(request.OutputPath, request.Overwrite, temp =>
        {
            try { loaded.Document.Save(temp, options); }
            finally { loaded.Resources.ThrowIfFailed(); }
        });
        var warnings = OutputWarnings(state, loaded, request.TargetFormatId);
        return new WordsConvertResult
        {
            Input = InfoProjection.Source(filePath, loaded),
            Output = BuildOutput(request.OutputPath, request.TargetFormatId, size),
            Pages = request.Pages?.Text,
            License = EnvelopeParts.License(state),
            Warnings = warnings,
        };
    }

    private WordsRenderResult RenderCore(string filePath, WordsRenderRequest request)
    {
        RenderPixelGuard.EnsureDpi(request.Dpi, 36, 1_200);
        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedDocument loaded = _loader.Open(filePath, request.Password);
        IReadOnlyList<int> pages = request.AllPages
            ? Enumerable.Range(1, loaded.Document.PageCount).ToArray()
            : request.Pages?.Resolve(loaded.Document.PageCount) ?? [1];
        using var transaction = new AtomicOutputSetWriter(_writer, Path.GetDirectoryName(request.OutputPath)!, "words-render");
        var outputs = new List<PageOutput>(pages.Count);
        foreach (int page in pages)
        {
            PageInfo info = loaded.Document.GetPageInfo(page - 1);
            long width = (long)Math.Ceiling(info.WidthInPoints / 72d * request.Dpi);
            long height = (long)Math.Ceiling(info.HeightInPoints / 72d * request.Dpi);
            RenderPixelGuard.EnsureFits(width, height, request.Dpi);
            string path = pages.Count == 1 ? request.OutputPath : PagePath(request.OutputPath, page);
            SaveOptions options = WordsSavePipeline.Options(request.TargetFormatId, pages: [page], dpi: request.Dpi);
            long size = transaction.Stage(path, request.Overwrite, temp => loaded.Document.Save(temp, options)).SizeBytes;
            outputs.Add(new PageOutput { Page = page, Output = BuildOutput(path, request.TargetFormatId, size) });
        }

        loaded.Resources.ThrowIfFailed();
        transaction.Commit();
        return new WordsRenderResult
        {
            Input = InfoProjection.Source(filePath, loaded),
            Outputs = outputs,
            Dpi = request.TargetFormatId == "svg" ? null : request.Dpi,
            License = EnvelopeParts.License(state),
            Warnings = OutputWarnings(state, loaded, request.TargetFormatId),
        };
    }

    private PreviewRenderOutcome RenderPreviewCore(
        string filePath,
        WordsPreviewRequest request,
        IPreviewArtifactSink artifacts)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        _ = _licenseGate.EnsureApplied();
        using LoadedDocument loaded = _loader.Open(filePath, request.Password);
        const string entry = "document.html";
        IReadOnlyList<PreviewPage> pages = RenderPreviewPages(
            loaded.Document,
            artifacts);
        artifacts.WriteText(entry, PreviewHtml(pages));
        return new PreviewRenderOutcome(entry, loaded.FormatId, new FileInfo(filePath).Length, InputWarnings(loaded));
    }

    private static IReadOnlyList<PreviewPage> RenderPreviewPages(
        Document document,
        IPreviewArtifactSink artifacts)
    {
        var pages = new List<PreviewPage>(document.PageCount);
        for (int page = 1; page <= document.PageCount; page++)
        {
            PageInfo info = document.GetPageInfo(page - 1);
            int pixelWidth = Pixels(info.WidthInPoints, PreviewDpi);
            int pixelHeight = Pixels(info.HeightInPoints, PreviewDpi);
            RenderPixelGuard.EnsureFits(pixelWidth, pixelHeight, PreviewDpi);
            string fileName = $"page-{page.ToString(CultureInfo.InvariantCulture)}.png";
            artifacts.Write(
                fileName,
                stream => document.Save(
                    stream,
                    WordsSavePipeline.Options("png", pages: [page], dpi: PreviewDpi)));
            pages.Add(new PreviewPage(
                page,
                fileName,
                Pixels(info.WidthInPoints, CssDpi),
                Pixels(info.HeightInPoints, CssDpi),
                pixelWidth,
                pixelHeight));
        }
        return pages;
    }

    private static string PreviewHtml(IReadOnlyList<PreviewPage> pages)
    {
        var html = new StringBuilder(
            "<!doctype html><html><head><meta charset=\"utf-8\">"
            + "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">"
            + "</head><body>");
        foreach (PreviewPage page in pages)
        {
            html.Append(CultureInfo.InvariantCulture, $"<div class=\"awpage words-raster-page\" data-page=\"{page.Number}\" style=\"width:{page.CssWidth}px;height:{page.CssHeight}px\">")
                .Append(CultureInfo.InvariantCulture, $"<img class=\"words-page-image\" src=\"/asset/{page.FileName}\" alt=\"Page {page.Number}\" width=\"{page.PixelWidth}\" height=\"{page.PixelHeight}\">")
                .Append("</div>");
        }
        return html.Append("</body></html>").ToString();
    }

    private static int Pixels(double points, int dpi) =>
        checked((int)Math.Ceiling(points / 72d * dpi));

    private WordsCreateResult CreateDocumentCore(NewDocumentRequest request)
    {
        LicenseState state = _licenseGate.EnsureApplied();
        using CreatedDocument created = Create(request);
        string formatId = FormatIdFromOutput(request.OutputPath);
        SaveOptions options = WordsSavePipeline.Options(formatId, request.EncryptPassword);
        long size = _writer.Write(request.OutputPath, request.Overwrite, temp => created.Save(temp, options));

        return new WordsCreateResult
        {
            Output = BuildOutput(request.OutputPath, formatId, size),
            License = EnvelopeParts.License(state),
            Warnings = CreationWarnings(state, created, formatId),
        };
    }

    private CreatedDocument Create(NewDocumentRequest request)
    {
        string? source = request.MarkdownPath ?? request.TemplatePath;
        if (source is not null)
        {
            LoadedDocument loaded = _loader.Open(source, null);
            try
            {
                if (request.MarkdownPath is not null) { ApplyMarkdownDocumentDesign(loaded.Document); }
                ApplyTitle(loaded.Document, request.Title);
                return new CreatedDocument(loaded.Document, loaded);
            }
            catch
            {
                loaded.Dispose();
                throw;
            }
        }

        if (request.TextPath is not null)
        {
            var document = new Document();
            var builder = new DocumentBuilder(document);
            builder.Write(_inputs.ReadTextFile(request.TextPath));
            ApplyTitle(document, request.Title);
            return new CreatedDocument(document, null);
        }

        var blank = new Document();
        ApplyTitle(blank, request.Title);
        return new CreatedDocument(blank, null);
    }

    /// <summary>
    /// Gives Markdown-authored documents a restrained, readable native Word
    /// design. Markdown carries semantic heading/list/table information but no
    /// dependable page design; leaving the engine defaults untouched produces
    /// technically valid documents whose hierarchy and page density vary with
    /// the machine. Only semantic styles and page setup are changed here, so
    /// explicitly formatted Markdown content remains explicit.
    /// </summary>
    private static void ApplyMarkdownDocumentDesign(Document document)
    {
        foreach (Section section in document.Sections)
        {
            section.PageSetup.TopMargin = 54;
            section.PageSetup.RightMargin = 58;
            section.PageSetup.BottomMargin = 54;
            section.PageSetup.LeftMargin = 58;
        }

        Style normal = document.Styles[StyleIdentifier.Normal];
        normal.Font.Name = "Arial";
        normal.Font.Size = 10.5;
        normal.Font.Color = System.Drawing.Color.FromArgb(31, 41, 55);
        normal.ParagraphFormat.SpaceAfter = 6;
        normal.ParagraphFormat.LineSpacingRule = LineSpacingRule.Multiple;
        normal.ParagraphFormat.LineSpacing = 13.8;
        normal.ParagraphFormat.WidowControl = true;

        ApplyHeadingStyle(document, StyleIdentifier.Heading1, 24, 18, 8);
        ApplyHeadingStyle(document, StyleIdentifier.Heading2, 17, 14, 6);
        ApplyHeadingStyle(document, StyleIdentifier.Heading3, 13, 10, 4);

        Style quote = document.Styles[StyleIdentifier.Quote];
        quote.Font.Name = "Arial";
        quote.Font.Size = 10;
        quote.Font.Color = System.Drawing.Color.FromArgb(71, 85, 105);
        quote.ParagraphFormat.LeftIndent = 18;
        quote.ParagraphFormat.RightIndent = 12;
        quote.ParagraphFormat.SpaceBefore = 6;
        quote.ParagraphFormat.SpaceAfter = 8;
    }

    private static void ApplyHeadingStyle(
        Document document,
        StyleIdentifier identifier,
        double size,
        double before,
        double after)
    {
        Style style = document.Styles[identifier];
        style.Font.Name = "Arial";
        style.Font.Size = size;
        style.Font.Bold = true;
        style.Font.Color = System.Drawing.Color.FromArgb(15, 42, 61);
        style.ParagraphFormat.SpaceBefore = before;
        style.ParagraphFormat.SpaceAfter = after;
        style.ParagraphFormat.KeepWithNext = true;
        style.ParagraphFormat.KeepTogether = true;
        style.ParagraphFormat.WidowControl = true;
    }

    private static void ApplyTitle(Document document, string? title)
    {
        if (title is not null)
        {
            document.BuiltInDocumentProperties.Title = title;
        }
    }

    private static IReadOnlyList<Warning>? CreationWarnings(
        LicenseState state,
        CreatedDocument created,
        string format)
    {
        var extra = new List<Warning>();
        if (created.RemoteResourcesBlocked > 0)
        {
            extra.Add(RemoteWarning(created.RemoteResourcesBlocked));
        }

        if (created.HasMacros && format is not "docm" and not "dotm")
        {
            extra.Add(new Warning { Code = WordsDiagnostics.MacrosDropped, Message = "The source template contains macros which the target format does not preserve.", Hint = "Create a docm/dotm output to preserve macros." });
        }

        if (created.WasSigned)
        {
            extra.Add(new Warning { Code = WarningCodes.SignatureInvalidated, Message = "Creating from the signed source invalidates its digital signature.", Hint = "Sign the produced document after review." });
        }

        return Combine(EnvelopeParts.OutputWarnings(state), extra);
    }

    private static string PagePath(string path, int page) =>
        Path.Combine(
            Path.GetDirectoryName(path)!,
            $"{Path.GetFileNameWithoutExtension(path)}.p{page}{Path.GetExtension(path)}");

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..length] + "…";
    private static string FormatIdFromOutput(string path)
    {
        string extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return extension switch { "xml" => "flatopc", "htm" => "html", _ => extension };
    }

    private sealed record CreatedDocument(Document Document, LoadedDocument? Source) : IDisposable
    {
        internal int RemoteResourcesBlocked => Source?.RemoteResourcesBlocked ?? 0;
        internal bool HasMacros => Source?.Format.HasMacros ?? false;
        internal bool WasSigned => Source?.Format.HasDigitalSignature ?? false;

        internal void Save(string path, SaveOptions options)
        {
            try { Document.Save(path, options); }
            finally { Source?.Resources.ThrowIfFailed(); }
        }

        public void Dispose()
        {
            if (Source is not null) { Source.Dispose(); }
            else { Document.Cleanup(); }
        }
    }

    private sealed record PreviewPage(
        int Number,
        string FileName,
        int CssWidth,
        int CssHeight,
        int PixelWidth,
        int PixelHeight);
}
