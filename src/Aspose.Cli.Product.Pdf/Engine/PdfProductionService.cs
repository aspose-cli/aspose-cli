using System.Globalization;
using System.Net;
using System.Text;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Product.Pdf.Engine.Mapping;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Preview;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Devices;
using Aspose.Pdf.Text;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;
using DrawingImageFormat = Aspose.Pdf.Drawing.ImageFormat;

using static Aspose.Cli.Product.Pdf.Engine.PdfArtifactSupport;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Owns PDF rendering, creation, merge, and preview production.</summary>
internal sealed class PdfProductionService
{
    private const int PreviewDpi = 120;
    private readonly ILicenseGate _licenseGate;
    private readonly ResourceBudgetLedger _resourceBudgets;
    private readonly SafeFileWriter _writer;
    private readonly PdfDocumentLoader _loader;

    internal PdfProductionService(
        ILicenseGate licenseGate,
        ResourceBudgetLedger resourceBudgets,
        SafeFileWriter writer,
        PdfDocumentLoader loader)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        _resourceBudgets = resourceBudgets;
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _loader = loader;
    }

    /// <inheritdoc />
    internal PdfRenderResult Render(string filePath, PdfRenderRequest request) =>
        PdfErrorTranslator.Execute("render", () => RenderCore(filePath, request));

    /// <inheritdoc />
    internal PdfWriteResult Create(NewPdfRequest request) =>
        PdfErrorTranslator.Execute("create", () => CreateCore(request));

    /// <inheritdoc />
    internal PdfWriteResult Merge(PdfMergeRequest request) =>
        PdfErrorTranslator.Execute("merge", () => MergeCore(request));

    /// <inheritdoc />
    internal PreviewRenderOutcome RenderPreview(
        string filePath,
        PdfPreviewRequest request,
        IPreviewArtifactSink artifacts) =>
        PdfErrorTranslator.Execute(
            "preview",
            () => RenderPreviewCore(filePath, request, artifacts));

    private PreviewRenderOutcome RenderPreviewCore(
        string filePath,
        PdfPreviewRequest request,
        IPreviewArtifactSink artifacts)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        _ = _licenseGate.EnsureApplied();
        using LoadedPdf loaded = _loader.Open(filePath, request.Password);

        var pages = new StringBuilder();
        for (int pageNumber = 1; pageNumber <= loaded.Document.Pages.Count; pageNumber++)
        {
            Page page = loaded.Document.Pages[pageNumber];
            long pixelWidth = (long)Math.Ceiling(page.Rect.Width / 72d * PreviewDpi);
            long pixelHeight = (long)Math.Ceiling(page.Rect.Height / 72d * PreviewDpi);
            RenderPixelGuard.EnsureFits(pixelWidth, pixelHeight, PreviewDpi);

            string imageName = string.Create(
                CultureInfo.InvariantCulture,
                $"page-{pageNumber:0000}.png");
            artifacts.Write(
                imageName,
                stream => RenderPage(
                    loaded.Document,
                    pageNumber,
                    "png",
                    PreviewDpi,
                    stream));

            string size = string.Create(
                CultureInfo.InvariantCulture,
                $"{page.Rect.Width:0.##} × {page.Rect.Height:0.##} pt");
            pages.Append("<figure class=\"pdf-snapshot-page\" data-page=\"")
                .Append(pageNumber.ToString(CultureInfo.InvariantCulture))
                .Append("\"><div class=\"pdf-page-paper\"><img src=\"/asset/")
                .Append(imageName)
                .Append("\" alt=\"PDF page ")
                .Append(pageNumber.ToString(CultureInfo.InvariantCulture))
                .Append("\" width=\"")
                .Append(pixelWidth.ToString(CultureInfo.InvariantCulture))
                .Append("\" height=\"")
                .Append(pixelHeight.ToString(CultureInfo.InvariantCulture))
                .Append("\"></div><figcaption>Page ")
                .Append(pageNumber.ToString(CultureInfo.InvariantCulture))
                .Append(" · ")
                .Append(WebUtility.HtmlEncode(size))
                .Append("</figcaption></figure>");
        }

        const string entry = "document.html";
        string title = WebUtility.HtmlEncode(Path.GetFileName(filePath));
        string pageCount = loaded.Document.Pages.Count.ToString(CultureInfo.InvariantCulture);
        string html = "<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\""
            + " content=\"width=device-width,initial-scale=1\"><title>" + title + "</title></head>"
            + "<body><main class=\"pdf-snapshot\" role=\"document\" aria-label=\"PDF pages\""
            + " data-page-count=\"" + pageCount + "\">" + pages + "</main></body></html>";
        artifacts.WriteText(entry, html);
        return new PreviewRenderOutcome(entry, "pdf", new FileInfo(filePath).Length);
    }

    private PdfRenderResult RenderCore(string filePath, PdfRenderRequest request)
    {
        if (!PdfFormats.IsRender(request.TargetFormatId))
        {
            throw CliErrors.FormatUnsupported(request.TargetFormatId, PdfFormats.RenderIds);
        }
        RenderPixelGuard.EnsureDpi(request.Dpi, 36, 1_200);

        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedPdf loaded = _loader.Open(filePath, request.Password);
        IReadOnlyList<int> pages = request.AllPages
            ? Enumerable.Range(1, loaded.Document.Pages.Count).ToArray()
            : request.Pages?.Resolve(loaded.Document.Pages.Count) ?? [1];
        string outputDirectory = Path.GetDirectoryName(Path.GetFullPath(request.OutputPath))!;
        using var writer = new AtomicOutputSetWriter(_writer, outputDirectory, "pdf-render");
        var staged = new List<(int Page, string Path)>();
        foreach (int pageNumber in pages)
        {
            Page page = loaded.Document.Pages[pageNumber];
            if (request.TargetFormatId != "svg")
            {
                long width = (long)Math.Ceiling(page.Rect.Width / 72d * request.Dpi);
                long height = (long)Math.Ceiling(page.Rect.Height / 72d * request.Dpi);
                RenderPixelGuard.EnsureFits(width, height, request.Dpi);
            }

            string path = pages.Count == 1 ? Path.GetFullPath(request.OutputPath) : PagePath(request.OutputPath, pageNumber);
            writer.Stage(path, request.Overwrite, stagedPath =>
            {
                using FileStream stream = File.Create(stagedPath);
                RenderPage(
                    loaded.Document,
                    pageNumber,
                    request.TargetFormatId,
                    request.Dpi,
                    stream);
            });
            staged.Add((pageNumber, path));
        }

        IReadOnlyList<long> sizes = writer.Commit();
        return new PdfRenderResult
        {
            Input = PdfInfoProjection.Source(filePath),
            Outputs = staged.Select((item, index) => new PdfPageOutput
            {
                Page = item.Page,
                Output = BuildOutput(item.Path, request.TargetFormatId, sizes[index]),
            }).ToArray(),
            Dpi = request.TargetFormatId == "svg" ? null : request.Dpi,
            License = EnvelopeParts.License(state),
            Warnings = OutputWarnings(state),
        };
    }

    private static void RenderPage(
        Document document,
        int pageNumber,
        string format,
        int dpi,
        Stream stream)
    {
        Page page = document.Pages[pageNumber];
        switch (format)
        {
            case "png":
                new PngDevice(new Resolution(dpi)).Process(page, stream);
                break;
            case "jpeg":
                new JpegDevice(new Resolution(dpi), 95).Process(page, stream);
                break;
            case "svg":
                using (Document selected = Select(document, [pageNumber]))
                {
                    selected.Save(stream, SaveFormat.Svg);
                }

                break;
            default:
                throw CliErrors.FormatUnsupported(format, PdfFormats.RenderIds);
        }
    }

    private PdfWriteResult CreateCore(NewPdfRequest request)
    {
        EnsurePdfOutput(request.OutputPath);
        int sources = request.ImagePaths is { Count: > 0 } ? 1 : 0;
        sources += request.HtmlPath is null ? 0 : 1;
        sources += request.TextPath is null ? 0 : 1;
        if (sources != 1)
        {
            throw CliErrors.Usage(["Choose exactly one image list, HTML file, text file or Markdown file."]);
        }

        EnsureCreationInputs(_resourceBudgets, request);
        LicenseState state = _licenseGate.EnsureApplied();
        using LocalDocumentResourceLoader? resources = request.HtmlPath is { } htmlPath
            ? new LocalDocumentResourceLoader(htmlPath, _resourceBudgets) : null;
        using Document document = request.ImagePaths is { Count: > 0 } images
            ? CreateFromImages(images, request)
            : request.HtmlPath is not null
                ? CreateFromHtml(request.HtmlPath, request, resources!)
                : CreateFromText(request.TextPath!, request.Markdown, request);
        resources?.ThrowIfFailed();
        long size = _writer.Write(request.OutputPath, request.Overwrite, path =>
        {
            try { document.Save(path); }
            finally { resources?.ThrowIfFailed(); }
        });
        int blocked = resources?.OmittedCount ?? 0;
        var warnings = OutputWarnings(state)?.ToList() ?? [];
        if (blocked > 0)
        {
            warnings.Add(new Warning
            {
                Code = WarningCodes.RemoteResourcesBlocked,
                AffectsCompleteness = true,
                Message = $"{blocked} external HTML resource(s) were omitted.",
                Hint = "Copy the resource beside the HTML input, or use a separately verified local cache.",
            });
        }

        return new PdfWriteResult
        {
            Action = "create",
            Output = BuildOutput(request.OutputPath, "pdf", size),
            Inputs = CreationInputs(request),
            License = EnvelopeParts.License(state),
            Warnings = warnings.Count == 0 ? null : warnings,
        };
    }

    private static Document CreateFromImages(IReadOnlyList<string> paths, NewPdfRequest request)
    {
        var document = new Document();
        try
        {
            (double width, double height) = PageDimensions(request.PageSize);
            ValidateMargins(request.Margins, width, height);
            foreach (string path in paths)
            {
                if (!File.Exists(path))
                {
                    throw CliErrors.FileNotFound(path);
                }

                Page page = document.Pages.Add();
                page.SetPageSize(width, height);
                page.PageInfo.Margin = Margin(request.Margins);
                page.Paragraphs.Add(new Aspose.Pdf.Image
                {
                    File = Path.GetFullPath(path),
                    FixWidth = Math.Max(1, width - request.Margins.Left - request.Margins.Right),
                    FixHeight = Math.Max(1, height - request.Margins.Top - request.Margins.Bottom),
                });
            }

            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    private static Document CreateFromHtml(
        string path, NewPdfRequest request, LocalDocumentResourceLoader resources)
    {
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw CliErrors.FileNotFound(fullPath);
        }

        var loader = new PdfArtifactSupport.HtmlResourceLoader(resources);
        (double width, double height) = PageDimensions(request.PageSize);
        ValidateMargins(request.Margins, width, height);
        var options = new HtmlLoadOptions(Path.GetDirectoryName(fullPath))
        {
            PageInfo = new PageInfo
            {
                Width = width,
                Height = height,
                Margin = Margin(request.Margins),
            },
            CustomLoaderOfExternalResources = loader.Load,
        };
        Document? document = null;
        try
        {
            document = new Document(fullPath, options);
            resources.ThrowIfFailed();
            return document;
        }
        catch
        {
            document?.Dispose();
            resources.ThrowIfFailed();
            throw;
        }
    }

    private static Document CreateFromText(string path, bool markdown, NewPdfRequest request)
    {
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw CliErrors.FileNotFound(fullPath);
        }

        (double width, double height) = PageDimensions(request.PageSize);
        ValidateMargins(request.Margins, width, height);
        Document document = markdown
            ? new Document(fullPath, new MdLoadOptions())
            : new Document(fullPath, new TxtLoadOptions());
        foreach (Page page in document.Pages)
        {
            page.SetPageSize(width, height);
            page.PageInfo.Margin = Margin(request.Margins);
        }

        return document;
    }

    private PdfWriteResult MergeCore(PdfMergeRequest request)
    {
        EnsurePdfOutput(request.OutputPath);
        if (request.InputPaths.Count < 2)
        {
            throw CliErrors.Usage(["Pass at least two PDF inputs to merge."]);
        }

        LicenseState state = _licenseGate.EnsureApplied();
        var inputs = new List<SourceInfo>(request.InputPaths.Count);
        using var merged = new Document();
        foreach (string path in request.InputPaths)
        {
            using LoadedPdf loaded = _loader.Open(path, request.Password);
            int offset = merged.Pages.Count;
            foreach (Page page in loaded.Document.Pages)
            {
                merged.Pages.Add(page);
            }

            if (request.PreserveBookmarks)
            {
                CopyOutline(loaded.Document.Outlines, merged.Outlines, merged, offset);
            }

            inputs.Add(PdfInfoProjection.Source(path));
        }

        long size = _writer.Write(request.OutputPath, request.Overwrite, merged.Save);
        return new PdfWriteResult
        {
            Action = "merge",
            Output = BuildOutput(request.OutputPath, "pdf", size),
            Inputs = inputs,
            License = EnvelopeParts.License(state),
            Warnings = OutputWarnings(state),
        };
    }

}
