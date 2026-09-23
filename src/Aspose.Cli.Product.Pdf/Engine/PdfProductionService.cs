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
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Views;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Devices;
using Aspose.Pdf.Text;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;
using DrawingImageFormat = Aspose.Pdf.Drawing.ImageFormat;


namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Owns PDF conversion, rendering, creation, merge, and preview production.</summary>
internal sealed class PdfProductionService
{
    private const int ImageDpi = 192;
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

    /// <summary>Renders the pages of one view, opening the document once.</summary>
    internal ViewManifest RenderView(
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts)
    {
        const int evidenceDpi = 150;
        const int displayDpi = 192;
        const double cssDpi = 96;
        ArgumentNullException.ThrowIfNull(artifacts);
        _ = _licenseGate.EnsureApplied();
        using LoadedPdf loaded = _loader.Open(filePath, request.Password);
        int dpi = request.Purpose == ViewPurpose.Display ? displayDpi : evidenceDpi;
        int total = loaded.Document.Pages.Count;
        int count = Math.Min(total, request.MaxParts);
        var parts = new List<ViewPart>(count);
        for (int pageNumber = 1; pageNumber <= count; pageNumber++)
        {
            Page page = loaded.Document.Pages[pageNumber];
            string file = string.Create(CultureInfo.InvariantCulture, $"page-{pageNumber:0000}.png");
            int number = pageNumber;
            artifacts.Write(
                file,
                stream => RenderPage(loaded.Document, number, "png", dpi, stream));
            parts.Add(new ViewPart
            {
                Id = string.Create(CultureInfo.InvariantCulture, $"page-{pageNumber}"),
                Label = string.Create(CultureInfo.InvariantCulture, $"Page {pageNumber}"),
                File = file,
                Kind = ViewPartKinds.Image,
                Width = Math.Max(1, (int)Math.Ceiling(page.Rect.Width / 72d * cssDpi)),
                Height = Math.Max(1, (int)Math.Ceiling(page.Rect.Height / 72d * cssDpi)),
                Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["size"] = string.Create(
                        CultureInfo.InvariantCulture,
                        $"{page.Rect.Width:0.##} × {page.Rect.Height:0.##} pt"),
                },
            });
        }

        return new ViewManifest
        {
            View = PdfViews.Pages,
            SourceFormat = "pdf",
            SourceSizeBytes = new FileInfo(filePath).Length,
            TotalParts = total,
            Parts = parts,
        };
    }

    internal PdfRenderResult Render(string filePath, PdfRenderRequest request)
    {
        if (!PdfFormats.IsRender(request.TargetFormatId))
        {
            throw CliErrors.FormatUnsupported(request.TargetFormatId, PdfFormats.RenderIds);
        }

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
            string path = pages.Count == 1
                ? Path.GetFullPath(request.OutputPath)
                : PartOutputPath.For(request.OutputPath, PartOutputPath.Page, pageNumber);
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
            Warnings = EnvelopeParts.OutputWarnings(state),
        };
    }

    /// <summary>
    /// Writes one page as an image. Every page raster passes the shared pixel guard here,
    /// before the engine allocates the bitmap.
    /// </summary>
    private void RenderPage(
        Document document,
        int pageNumber,
        string format,
        int dpi,
        Stream stream)
    {
        Page page = document.Pages[pageNumber];
        if (format != "svg")
        {
            EnsurePageFits(page, dpi);
        }

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

    private void EnsurePageFits(Page page, int dpi) =>
        RenderPixelGuard.EnsureFits(
            _resourceBudgets,
            (long)Math.Ceiling(page.Rect.Width / 72d * dpi),
            (long)Math.Ceiling(page.Rect.Height / 72d * dpi),
            dpi,
            "Render fewer or smaller pages, or lower --dpi.");

    internal PdfWriteResult Create(NewPdfRequest request)
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
        var warnings = EnvelopeParts.OutputWarnings(state)?.ToList() ?? [];
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

        var loader = new HtmlResourceLoader(resources);
        (double width, double height) = PageDimensions(request.PageSize);
        ValidateMargins(request.Margins, width, height);
        // Use the native directory form of the verified origin, retaining its trailing separator.
        var options = new HtmlLoadOptions(new Uri(resources.BaseUri).LocalPath)
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
        var pageInfo = new PageInfo { Width = width, Height = height, Margin = Margin(request.Margins) };
        return markdown
            ? new Document(fullPath, new MdLoadOptions { PageInfo = pageInfo })
            : CreateFromPlainText(fullPath, pageInfo);
    }

    /// <summary>
    /// Lays plain text out line by line on pages of the requested size and margins. The
    /// SDK's text loader has no page settings and fixes its own page and origin, so
    /// resizing its pages afterwards leaves the text where the loader put it.
    /// </summary>
    private static Document CreateFromPlainText(string path, PageInfo pageInfo)
    {
        var document = new Document();
        try
        {
            Page page = document.Pages.Add();
            page.SetPageSize(pageInfo.Width, pageInfo.Height);
            page.PageInfo.Margin = pageInfo.Margin;
            foreach (string line in File.ReadLines(path))
            {
                page.Paragraphs.Add(new TextFragment(ExpandTabs(line)));
            }

            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    /// <summary>Replaces tabs with spaces up to the next eight-column stop, as a terminal shows them.</summary>
    private static string ExpandTabs(string line)
    {
        if (!line.Contains('\t', StringComparison.Ordinal))
        {
            return line;
        }

        var expanded = new StringBuilder(line.Length + 8);
        foreach (char character in line)
        {
            if (character == '\t')
            {
                expanded.Append(' ', 8 - (expanded.Length % 8));
            }
            else
            {
                expanded.Append(character);
            }
        }

        return expanded.ToString();
    }

    internal PdfWriteResult Merge(PdfMergeRequest request)
    {
        EnsurePdfOutput(request.OutputPath);
        if (request.InputPaths.Count < 2)
        {
            throw CliErrors.Usage(["Pass at least two PDF inputs to merge."]);
        }

        LicenseState state = _licenseGate.EnsureApplied();
        var inputs = new List<SourceInfo>(request.InputPaths.Count);
        using var merged = new Document();
        int bookmarks = 0;
        int brokenInputLinks = 0;
        int namedDestinations = 0;
        foreach (string path in request.InputPaths)
        {
            using LoadedPdf loaded = _loader.Open(path, request.Password);
            Document source = loaded.Document;
            brokenInputLinks += PdfNavigationCensus.Unresolved(source).Links;
            // The merged document carries no named destinations, so every working one is lost.
            namedDestinations += source.NamedDestinations.Names.Count(name =>
                PdfNavigationCensus.Resolves(source, source.NamedDestinations[name]));
            int offset = merged.Pages.Count;
            foreach (Page page in source.Pages)
            {
                merged.Pages.Add(page);
            }

            if (request.PreserveBookmarks)
            {
                bookmarks += CopyOutline(source, source.Outlines, merged.Outlines, merged, offset);
            }

            inputs.Add(PdfInfoProjection.Source(path));
        }

        var navigation = new PdfNavigationCensus(
            bookmarks,
            Math.Max(0, PdfNavigationCensus.Unresolved(merged).Links - brokenInputLinks),
            namedDestinations);
        long size = _writer.Write(request.OutputPath, request.Overwrite, merged.Save);
        List<Warning> warnings = [.. EnvelopeParts.OutputWarnings(state) ?? []];
        if (navigation.ToWarning(
                "lost their exact target: merged bookmarks open their page at Fit zoom, and named destinations are not carried into the merged document",
                "Re-create location-sensitive bookmarks (add_bookmark) and links (add_link) on the merged PDF.")
            is { } degraded)
        {
            warnings.Add(degraded);
        }

        return new PdfWriteResult
        {
            Action = "merge",
            Output = BuildOutput(request.OutputPath, "pdf", size),
            Inputs = inputs,
            License = EnvelopeParts.License(state),
            Warnings = warnings.Count == 0 ? null : warnings,
        };
    }

    internal PdfConvertResult Convert(string filePath, PdfConvertRequest request)
    {
        if (!PdfFormats.IsConvert(request.TargetFormatId))
        {
            throw CliErrors.FormatUnsupported(request.TargetFormatId, PdfFormats.ConvertIds);
        }

        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedPdf loaded = _loader.Open(filePath, request.Password);
        IReadOnlyList<int> pages = request.Pages?.Resolve(loaded.Document.Pages.Count)
            ?? Enumerable.Range(1, loaded.Document.Pages.Count).ToArray();
        IReadOnlyList<OutputInfo> outputs = request.TargetFormatId switch
        {
            "png" or "jpeg" or "svg" => ConvertPages(loaded.Document, pages, request),
            "tiff" => [ConvertTiff(loaded.Document, pages, request)],
            "txt" => [ConvertText(loaded.Document, pages, request)],
            "pdfa-1b" or "pdfa-2b" or "pdfa-3b" => [ConvertPdfa(loaded.Document, pages, request)],
            _ => [ConvertDocument(loaded.Document, pages, request)],
        };

        List<Warning> warnings = [.. EnvelopeParts.OutputWarnings(state) ?? []];

        if (request.TargetFormatId is not ("xps" or "svg" or "png" or "jpeg" or "tiff"))
        {
            warnings.Add(new Warning
            {
                Code = WarningCodes.LossyConversion,
                Message = $"PDF conversion to {request.TargetFormatId} may not preserve every layout or interactive feature.",
                Hint = "Inspect the produced file before relying on exact pagination, forms or annotations.",
            });
        }

        return new PdfConvertResult
        {
            Input = PdfInfoProjection.Source(filePath),
            Outputs = outputs,
            Pages = request.Pages?.Text,
            License = EnvelopeParts.License(state),
            Warnings = warnings.Count == 0 ? null : warnings,
        };
    }

    private OutputInfo ConvertDocument(Document source, IReadOnlyList<int> pages, PdfConvertRequest request)
    {
        using Document selected = Select(source, pages);
        SaveFormat format = request.TargetFormatId switch
        {
            "docx" => SaveFormat.DocX,
            "xlsx" => SaveFormat.Excel,
            "pptx" => SaveFormat.Pptx,
            "html" => SaveFormat.Html,
            "epub" => SaveFormat.Epub,
            "md" => SaveFormat.Markdown,
            "xps" => SaveFormat.Xps,
            _ => throw CliErrors.FormatUnsupported(request.TargetFormatId, PdfFormats.ConvertIds),
        };
        long size = _writer.Write(
            request.OutputPath,
            request.Overwrite,
            temp =>
            {
                if (request.TargetFormatId == "html")
                {
                    selected.Save(temp, new HtmlSaveOptions
                    {
                        PartsEmbeddingMode = HtmlSaveOptions.PartsEmbeddingModes.EmbedAllIntoHtml,
                    });
                }
                else
                {
                    selected.Save(temp, format);
                }
            });
        return BuildOutput(request.OutputPath, request.TargetFormatId, size);
    }

    private OutputInfo ConvertText(Document source, IReadOnlyList<int> pages, PdfConvertRequest request)
    {
        long size = _writer.Write(request.OutputPath, request.Overwrite, temp =>
            File.WriteAllText(temp, DocumentText(source, pages), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)));
        return BuildOutput(request.OutputPath, "txt", size);
    }

    private OutputInfo ConvertPdfa(Document source, IReadOnlyList<int> pages, PdfConvertRequest request)
    {
        using Document selected = Select(source, pages);
        PdfFormat format = request.TargetFormatId switch
        {
            "pdfa-1b" => PdfFormat.PDF_A_1B,
            "pdfa-2b" => PdfFormat.PDF_A_2B,
            "pdfa-3b" => PdfFormat.PDF_A_3B,
            _ => throw CliErrors.FormatUnsupported(request.TargetFormatId, PdfFormats.PdfaConvertIds),
        };

        long size = _writer.Write(request.OutputPath, request.Overwrite, temp =>
        {
            using var log = new MemoryStream();
            PdfComplianceLog.EnsureConverted(
                selected.Convert(log, format, ConvertErrorAction.Delete), log, request.TargetFormatId);
            selected.Save(temp);
        });
        return BuildOutput(request.OutputPath, request.TargetFormatId, size);
    }

    private IReadOnlyList<OutputInfo> ConvertPages(
        Document document,
        IReadOnlyList<int> pages,
        PdfConvertRequest request)
    {
        string outputDirectory = Path.GetDirectoryName(Path.GetFullPath(request.OutputPath))!;
        using var writer = new AtomicOutputSetWriter(_writer, outputDirectory, "pdf-convert");
        var paths = new List<string>(pages.Count);
        foreach (int pageNumber in pages)
        {
            string path = pages.Count == 1
                ? request.OutputPath
                : PartOutputPath.For(request.OutputPath, PartOutputPath.Page, pageNumber);
            writer.Stage(path, request.Overwrite, temp =>
            {
                using FileStream stream = File.Create(temp);
                RenderPage(document, pageNumber, request.TargetFormatId, ImageDpi, stream);
            });
            paths.Add(path);
        }

        IReadOnlyList<long> sizes = writer.Commit();
        return paths.Select((path, index) =>
            BuildOutput(path, request.TargetFormatId, sizes[index])).ToArray();
    }

    private OutputInfo ConvertTiff(
        Document source,
        IReadOnlyList<int> pages,
        PdfConvertRequest request)
    {
        foreach (int pageNumber in pages)
        {
            EnsurePageFits(source.Pages[pageNumber], ImageDpi);
        }

        using Document selected = Select(source, pages);
        long size = _writer.Write(request.OutputPath, request.Overwrite, temp =>
        {
            using FileStream stream = File.Create(temp);
            var device = new TiffDevice(new Resolution(ImageDpi), new TiffSettings());
            device.Process(selected, 1, selected.Pages.Count, stream);
        });
        return BuildOutput(request.OutputPath, "tiff", size);
    }

    private static MarginInfo Margin(PdfMargins margins) => new()
    {
        Top = margins.Top,
        Right = margins.Right,
        Bottom = margins.Bottom,
        Left = margins.Left,
    };

    private static void ValidateMargins(PdfMargins margins, double width, double height)
    {
        if (margins.Top < 0 || margins.Right < 0 || margins.Bottom < 0 || margins.Left < 0
            || margins.Left + margins.Right >= width
            || margins.Top + margins.Bottom >= height)
        {
            throw CliErrors.OptionInvalid(
                "--margins",
                "values must be non-negative and leave a positive content area",
                "Use smaller top,right,bottom,left values in points.");
        }
    }

    private static IReadOnlyList<SourceInfo> CreationInputs(NewPdfRequest request)
    {
        IEnumerable<string> paths = request.ImagePaths
            ?? (request.HtmlPath is not null ? [request.HtmlPath] : [request.TextPath!]);
        return paths.Select(path => new SourceInfo
        {
            Path = Path.GetFullPath(path),
            Format = Path.GetExtension(path).TrimStart('.').ToLowerInvariant(),
            SizeBytes = new FileInfo(path).Length,
        }).ToArray();
    }

    private static void EnsureCreationInputs(
        ResourceBudgetLedger resourceBudgets,
        NewPdfRequest request)
    {
        IReadOnlyList<string> paths = request.ImagePaths
            ?? (request.HtmlPath is not null ? [request.HtmlPath] : [request.TextPath!]);
        if (paths.Count > 1000)
        {
            throw CliErrors.OptionInvalid(
                "--from-images",
                "more than 1000 inputs were requested",
                "Create smaller PDFs and merge them in bounded batches.");
        }

        foreach (string path in paths)
        {
            InputSizeGuard.Ensure(resourceBudgets, path);
        }
    }

    /// <summary>
    /// Copies an outline into the merged document, pointing each bookmark at its page with
    /// a Fit destination, and returns how many working bookmarks lost fidelity: a location
    /// or zoom other than Fit, or a named destination the merged document does not carry.
    /// </summary>
    private static int CopyOutline(
        Document source,
        IEnumerable<OutlineItemCollection> items,
        ICollection<OutlineItemCollection> target,
        Document document,
        int pageOffset)
    {
        int degraded = 0;
        foreach (OutlineItemCollection item in items)
        {
            var copied = new OutlineItemCollection(document.Outlines)
            {
                Title = item.Title,
                Bold = item.Bold,
                Italic = item.Italic,
                Color = item.Color,
            };
            IAppointment? destination = PdfNavigationCensus.Target(item.Destination, item.Action);
            int page = PdfNavigationCensus.DestinationPage(item);
            if (page > 0)
            {
                copied.Destination = new FitExplicitDestination(document.Pages[pageOffset + page]);
            }

            if (PdfNavigationCensus.Resolves(source, destination)
                && (page == 0 || destination is not FitExplicitDestination))
            {
                degraded++;
            }

            degraded += CopyOutline(source, item, copied, document, pageOffset);
            target.Add(copied);
        }

        return degraded;
    }

    private sealed class HtmlResourceLoader(LocalDocumentResourceLoader resources)
    {
        public LoadOptions.ResourceLoadingResult Load(string resourceUri)
        {
            if (resources.TryRead(resourceUri, out byte[] data))
            {
                return new LoadOptions.ResourceLoadingResult(data);
            }

            return new LoadOptions.ResourceLoadingResult(Array.Empty<byte>())
            {
                // Cancelling the custom loader would enable the SDK default loader.
                LoadingCancelled = false,
            };
        }
    }
}
