using System.Globalization;
using System.Text;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Views;
using Aspose.Words;
using Aspose.Words.Rendering;
using Aspose.Words.Saving;
using static Aspose.Cli.Product.Words.Engine.WordsEngineSupport;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>Owns document conversion, rendering, preview and creation.</summary>
internal sealed class WordsProductionService
{
    private const int CssDpi = 96;
    private const int EvidenceDpi = 150;
    private const int DisplayDpi = 192;
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

    /// <summary>Renders the fixed-layout pages of one view, opening the document once.</summary>
    internal ViewManifest RenderView(
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts) =>
        WordsErrorTranslator.Execute(
            "render",
            () => RenderViewCore(filePath, request, artifacts));

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

    private ViewManifest RenderViewCore(
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        _ = _licenseGate.EnsureApplied();
        using LoadedDocument loaded = _loader.Open(filePath, request.Password);
        Document document = loaded.Document;
        int dpi = request.Purpose == ViewPurpose.Display ? DisplayDpi : EvidenceDpi;
        int total = document.PageCount;
        int count = Math.Min(total, request.MaxParts);
        IReadOnlyList<IReadOnlyList<ViewElement>> layout = WordsViewLayout.Collect(document, count);
        var parts = new List<ViewPart>(count);
        for (int page = 1; page <= count; page++)
        {
            PageInfo info = document.GetPageInfo(page - 1);
            RenderPixelGuard.EnsureFits(
                Pixels(info.WidthInPoints, dpi),
                Pixels(info.HeightInPoints, dpi),
                dpi);
            string file = string.Create(CultureInfo.InvariantCulture, $"page-{page:0000}.png");
            int pageNumber = page;
            artifacts.Write(
                file,
                stream => document.Save(
                    stream,
                    WordsSavePipeline.Options("png", pages: [pageNumber], dpi: dpi)));
            parts.Add(new ViewPart
            {
                Id = string.Create(CultureInfo.InvariantCulture, $"page-{page}"),
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
            TotalParts = total,
            Parts = parts,
            Warnings = InputWarnings(loaded),
        };
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

    /// <summary>
    /// A template supplies styles, page setup, headers and footers; Markdown or
    /// text supplies the body. Content is imported with the destination's
    /// styles, so the template alone owns the look of the created document.
    /// </summary>
    private CreatedDocument Create(NewDocumentRequest request)
    {
        var sources = new List<LoadedDocument>();
        try
        {
            LoadedDocument? template = request.TemplatePath is null ? null : _loader.Open(request.TemplatePath, null);
            if (template is not null)
            {
                sources.Add(template);
            }

            Document document = template?.Document ?? new Document();
            Document? content = null;
            if (request.MarkdownPath is not null)
            {
                LoadedDocument markdown = _loader.Open(request.MarkdownPath, null);
                sources.Add(markdown);
                content = markdown.Document;
                KeepOnlyExpressedEmphasis(content);
            }
            else if (request.TextPath is not null)
            {
                content = new Document();
                new DocumentBuilder(content).Write(_inputs.ReadTextFile(request.TextPath));
            }

            if (content is not null)
            {
                ReplaceBody(document, content);
            }

            ApplyTitle(document, request.Title);
            return new CreatedDocument(document, template, sources);
        }
        catch
        {
            sources.ForEach(static source => source.Dispose());
            throw;
        }
    }

    /// <summary>
    /// The Markdown reader stores "no emphasis" as explicit false bold, italic
    /// and strike-through on every run, which would override the destination's
    /// heading styles. Only emphasis the Markdown actually expressed and each
    /// run's character style (inline code, hyperlinks) are kept.
    /// </summary>
    private static void KeepOnlyExpressedEmphasis(Document markdown)
    {
        foreach (Run run in markdown.GetChildNodes(NodeType.Run, isDeep: true).OfType<Run>())
        {
            KeepOnlyExpressedEmphasis(run.Font);
        }

        foreach (Paragraph paragraph in markdown.GetChildNodes(NodeType.Paragraph, isDeep: true).OfType<Paragraph>())
        {
            KeepOnlyExpressedEmphasis(paragraph.ParagraphBreakFont);
        }
    }

    private static void KeepOnlyExpressedEmphasis(Aspose.Words.Font font)
    {
        (bool bold, bool italic, bool strike, string characterStyle) =
            (font.Bold, font.Italic, font.StrikeThrough, font.StyleName);
        font.ClearFormatting();
        font.StyleName = characterStyle;
        if (bold) { font.Bold = true; }
        if (italic) { font.Italic = true; }
        if (strike) { font.StrikeThrough = true; }
    }

    private static void ReplaceBody(Document destination, Document content)
    {
        while (destination.Sections.Count > 1)
        {
            destination.LastSection.Remove();
        }

        Body body = destination.FirstSection.Body;
        body.RemoveAllChildren();
        Paragraph anchor = body.AppendParagraph(string.Empty);
        var builder = new DocumentBuilder(destination);
        builder.MoveTo(anchor);
        builder.InsertDocument(content, ImportFormatMode.UseDestinationStyles);
        if (body.Paragraphs.Count > 1 && body.LastParagraph is { HasChildNodes: false } trailing)
        {
            trailing.Remove();
        }
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

    private sealed record CreatedDocument(
        Document Document,
        LoadedDocument? Template,
        IReadOnlyList<LoadedDocument> Sources) : IDisposable
    {
        internal int RemoteResourcesBlocked => Sources.Sum(static source => source.RemoteResourcesBlocked);
        internal bool HasMacros => Template?.Format.HasMacros ?? false;
        internal bool WasSigned => Template?.Format.HasDigitalSignature ?? false;

        internal void Save(string path, SaveOptions options)
        {
            try { Document.Save(path, options); }
            finally
            {
                foreach (LoadedDocument source in Sources) { source.Resources.ThrowIfFailed(); }
            }
        }

        public void Dispose()
        {
            if (Template is null) { Document.Cleanup(); }
            foreach (LoadedDocument source in Sources) { source.Dispose(); }
        }
    }

}
