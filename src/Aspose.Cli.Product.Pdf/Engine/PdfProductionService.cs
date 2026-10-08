using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Views;
using Aspose.Pdf;
using Aspose.Pdf.Devices;
using Aspose.Pdf.Text;
using SkiaSharp;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;


namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Owns PDF conversion, rendering, creation, merge, and preview production.</summary>
internal sealed class PdfProductionService
{
    /// <summary>Marks the page number in a multi-page output name: <c>report.p3.png</c>.</summary>
    private const string PagePart = "p";

    private const int ImageDpi = 192;
    private readonly OutputPipeline<Document> _outputs;
    private readonly ResourceBudgetLedger _resourceBudgets;
    private readonly PdfDocumentLoader _loader;

    internal PdfProductionService(
        OutputPipeline<Document> outputs,
        ResourceBudgetLedger resourceBudgets,
        PdfDocumentLoader loader)
    {
        _outputs = outputs ?? throw new ArgumentNullException(nameof(outputs));
        _resourceBudgets = resourceBudgets;
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
        _ = _outputs.License;
        using LoadedPdf loaded = _loader.Open(filePath, request.Password);
        bool evidence = request.Purpose != ViewPurpose.Display;
        int dpi = evidence ? evidenceDpi : displayDpi;
        int total = loaded.Document.Pages.Count;
        int count = Math.Min(total, request.MaxPartCount);
        var parts = new List<ViewPart>(count);
        for (int pageNumber = 1; pageNumber <= count; pageNumber++)
        {
            Page page = loaded.Document.Pages[pageNumber];
            string file = string.Create(CultureInfo.InvariantCulture, $"page-{pageNumber:0000}.png");
            int number = pageNumber;
            artifacts.Write(
                file,
                stream =>
                {
                    if (evidence)
                    {
                        RenderEvidencePage(loaded.Document, number, dpi, stream);
                    }
                    else
                    {
                        RenderPage(loaded.Document, number, "png", dpi, stream);
                    }
                });
            parts.Add(new ViewPart
            {
                Id = PdfViews.PagePart(pageNumber),
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
            SourceEncrypted = loaded.Document.IsEncrypted,
            TotalPartCount = total,
            Parts = parts,
        };
    }

    internal PdfRenderResult Render(string filePath, PdfRenderRequest request)
    {
        PdfRenderGrid? grid = RenderGrid(request);
        LicenseState state = _outputs.License;
        using LoadedPdf loaded = _loader.Open(filePath, request.Password);
        IReadOnlyList<int> pages = request.AllPages
            ? Enumerable.Range(1, loaded.Document.Pages.Count).ToArray()
            : request.Pages?.Resolve(loaded.Document.Pages.Count) ?? [1];
        string outputDirectory = request.Output.Directory;
        using OutputSet<Document> writer = _outputs.BeginSet([outputDirectory], "pdf-render");
        var staged = new List<(int Page, string Path)>();
        foreach (int pageNumber in pages)
        {
            string path = request.Output.Part(PagePart, pageNumber, pages.Count);
            writer.Stage(path, request.Output.Overwrite, loaded.Document, stagedPath =>
            {
                using FileStream stream = File.Create(stagedPath);
                RenderPage(
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

    /// <summary>
    /// Writes one page as an image. Every page raster passes the shared pixel guard here,
    /// before the engine allocates the bitmap. A <paramref name="grid"/> is drawn on the raster,
    /// never into the document.
    /// </summary>
    private void RenderPage(
        Document document,
        int pageNumber,
        string format,
        int dpi,
        Stream stream,
        PdfRenderGrid? grid = null)
    {
        Page page = document.Pages[pageNumber];
        if (format != "svg")
        {
            EnsurePageFits(page, dpi);
        }

        if (grid is not null)
        {
            using var raster = new MemoryStream();
            new PngDevice(new Resolution(dpi)).Process(page, raster);
            raster.Position = 0;
            Rectangle box = page.GetPageRect(considerRotation: true);
            PdfGridOverlay.Draw(raster, grid, box.Width, box.Height, dpi, format, stream);
            return;
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
                throw new InvalidOperationException($"'{format}' is not a PDF render format.");
        }
    }

    /// <summary>
    /// PDF-RENDER-THIN-GLYPH: the engine drops thin glyph strokes, such as the underscores of a
    /// signature line, at some positions below about 300 DPI. Review evidence is rendered at twice
    /// its resolution and scaled down by averaging, so such a stroke shows as grey at the
    /// evidence size. A page whose double-size raster exceeds the pixel budget renders directly.
    /// </summary>
    private void RenderEvidencePage(Document document, int pageNumber, int dpi, Stream stream)
    {
        Page page = document.Pages[pageNumber];
        int sampled = dpi * 2;
        (long width, long height) = PagePixels(page, sampled);
        long maxPixels = _resourceBudgets.Limit(ResourceBudgetKinds.RasterPixels);
        if (width <= 0 || height <= 0 || width > maxPixels / height)
        {
            RenderPage(document, pageNumber, "png", dpi, stream);
            return;
        }

        using var raster = new MemoryStream();
        new PngDevice(new Resolution(sampled)).Process(page, raster);
        raster.Position = 0;
        using SKBitmap full = SKBitmap.Decode(raster)
            ?? throw new InvalidOperationException("The rendered page image could not be decoded.");
        // At exactly half the size, linear filtering averages each 2 x 2 block of pixels.
        var info = new SKImageInfo((full.Width + 1) / 2, (full.Height + 1) / 2, full.ColorType, full.AlphaType);
        using SKBitmap scaled = full.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None))
            ?? throw new InvalidOperationException("The rendered page image could not be scaled.");
        using SKData encoded = scaled.Encode(SKEncodedImageFormat.Png, 100);
        encoded.SaveTo(stream);
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

    private void EnsurePageFits(Page page, int dpi)
    {
        (long width, long height) = PagePixels(page, dpi);
        RenderPixelGuard.EnsureFits(_resourceBudgets, width, height, dpi, "Render fewer or smaller pages, or lower --dpi.");
    }

    /// <summary>The pixel size of <paramref name="page"/> rendered at <paramref name="dpi"/>.</summary>
    private static (long Width, long Height) PagePixels(Page page, int dpi) =>
        ((long)Math.Ceiling(page.Rect.Width / 72d * dpi), (long)Math.Ceiling(page.Rect.Height / 72d * dpi));

    internal PdfWriteResult Create(NewPdfRequest request)
    {
        int sources = request.ImagePaths is { Count: > 0 } ? 1 : 0;
        sources += request.HtmlPath is null ? 0 : 1;
        sources += request.TextPath is null ? 0 : 1;
        if (sources != 1)
        {
            throw CliErrors.Usage(["Choose exactly one image list, HTML file, text file or Markdown file."]);
        }

        if (request.AllowNetworkResources && request.HtmlPath is null)
        {
            throw CliErrors.OptionInvalid(
                "--allow-network-resources",
                "it applies only to --from-html",
                "The Markdown importer follows local file references inside fetched resources without any resource hook, so network resources stay refused for Markdown; convert the Markdown to HTML first.");
        }

        byte[]? html = EnsureCreationInputs(_resourceBudgets, request);
        // The Markdown importer resolves the Markdown's relative references against the
        // working directory and reads files itself; the check holds them until the save.
        bool fromMarkdown = request.TextPath is { } textPath
            && string.Equals(Path.GetExtension(textPath), ".md", StringComparison.OrdinalIgnoreCase);
        using MarkdownImportResources? markdown = fromMarkdown && request.TextPath is { } markdownPath
            ? new MarkdownImportResources(markdownPath, _resourceBudgets, Environment.CurrentDirectory) : null;
        LicenseState state = _outputs.License;
        using HtmlImportResources? resources = request.HtmlPath is { } htmlPath
            ? new HtmlImportResources(htmlPath, _resourceBudgets, request.AllowNetworkResources) : null;
        using Document document = request.ImagePaths is { Count: > 0 } images
            ? CreateFromImages(images, request, _resourceBudgets)
            : request.HtmlPath is not null
                ? CreateFromHtml(request.HtmlPath, request, resources!)
                : CreateFromText(request.TextPath!, fromMarkdown, request);
        resources?.ThrowIfFailed();
        if (request.HtmlPath is not null || fromMarkdown)
        {
            // Both importers set the title, author and subject to a placeholder
            // (PDF-IMPORT-INFO-PLACEHOLDER); keep only the title the HTML states.
            document.Info.Title = html is null ? string.Empty : HtmlDocumentTitle.Read(html) ?? string.Empty;
            document.Info.Author = string.Empty;
            document.Info.Subject = string.Empty;
        }

        long size = _outputs.Write(request.Output.Path, request.Output.Overwrite, document, path =>
        {
            try { document.Save(path); }
            finally { resources?.ThrowIfFailed(); }
        });
        var warnings = new List<Warning>();
        warnings.AddRange(resources?.Warnings ?? []);
        if (html is not null && FormLosses(document, html) is { } losses)
        {
            warnings.Add(losses);
        }

        return new PdfWriteResult
        {
            Action = "create",
            Output = BuildOutput(request.Output.Path, "pdf", size),
            Inputs = CreationInputs(request),
            License = EnvelopeParts.License(state),
            Warnings = warnings.Count == 0 ? null : warnings,
        };
    }

    /// <summary>
    /// One page per image. The page takes the named size in the image's orientation (landscape
    /// for an image wider than it is tall), and the image keeps its aspect ratio: it is scaled to
    /// fill the margin box in one dimension and centred in the other. The margins must leave a
    /// content area on each page in the orientation it takes.
    /// </summary>
    private static Document CreateFromImages(
        IReadOnlyList<string> paths, NewPdfRequest request, ResourceBudgetLedger resourceBudgets)
    {
        var document = new Document();
        try
        {
            (double portraitWidth, double portraitHeight) = PdfPageSizes.Dimensions(request.PageSize);
            PdfMargins margins = request.Margins;
            foreach (string path in paths)
            {
                if (!File.Exists(path))
                {
                    throw CliErrors.FileNotFound(path);
                }

                string fullPath = Path.GetFullPath(path);
                (double imageWidth, double imageHeight) = PdfImageSize.Read(resourceBudgets.Inputs, fullPath);
                bool landscape = imageWidth > imageHeight;
                (double width, double height) = landscape
                    ? (portraitHeight, portraitWidth)
                    : (portraitWidth, portraitHeight);
                ValidateMargins(margins, width, height, $"the {(landscape ? "landscape" : "portrait")} page of {Path.GetFileName(path)}");
                double boxWidth = width - margins.Left - margins.Right;
                double boxHeight = height - margins.Top - margins.Bottom;
                double scale = Math.Min(boxWidth / imageWidth, boxHeight / imageHeight);
                double left = margins.Left + Math.Max(0, boxWidth - imageWidth * scale) / 2;
                double top = margins.Top + Math.Max(0, boxHeight - imageHeight * scale) / 2;

                // The layout places the image at the top-left margins and moves an image taller
                // than the room below them to the next page, again on every page: an image that
                // fills the margin box but comes out a rounding error taller than the room the
                // engine computes would never stop adding pages. So the right and bottom margins
                // are left to the image's own size, and the image is never larger than the room
                // from its corner to the page edges.
                Page page = document.Pages.Add();
                page.SetPageSize(width, height);
                page.PageInfo.Margin = new MarginInfo { Top = top, Left = left, Right = 0, Bottom = 0 };
                page.Paragraphs.Add(new Aspose.Pdf.Image
                {
                    File = fullPath,
                    FixWidth = Math.Min(Math.Max(1, imageWidth * scale), width - left),
                    FixHeight = Math.Min(Math.Max(1, imageHeight * scale), height - top),
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
        string path, NewPdfRequest request, HtmlImportResources resources)
    {
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw CliErrors.FileNotFound(fullPath);
        }

        (double width, double height) = PdfPageSizes.Dimensions(request.PageSize);
        ValidateMargins(request.Margins, width, height);
        var options = new HtmlLoadOptions(resources.BaseDirectory)
        {
            PageInfo = new PageInfo
            {
                Width = width,
                Height = height,
                Margin = Margin(request.Margins),
            },
            CustomLoaderOfExternalResources = resources.Load,
        };
        Document? document = null;
        try
        {
            // The importer reads the whole HTML while it constructs the document.
            using (FileStream stream = InputFiles.OpenRead(fullPath))
            {
                document = new Document(stream, options);
            }

            resources.ThrowIfFailed();
            // The importer gives a check box neither a border nor a border colour, so its
            // appearance draws no box and an unchecked box shows nothing (PDF-HTML-CHECKBOX-BOX).
            // A black one-point border, as the importer gives radio buttons, makes the engine draw it.
            foreach (Aspose.Pdf.Forms.CheckboxField box in document.Form.Fields.OfType<Aspose.Pdf.Forms.CheckboxField>())
            {
                if (box.Characteristics.Border.IsEmpty)
                {
                    box.Characteristics.Border = System.Drawing.Color.Black;
                    box.Border ??= new Aspose.Pdf.Annotations.Border(box) { Width = 1 };
                }
            }

            return document;
        }
        catch
        {
            document?.Dispose();
            resources.ThrowIfFailed();
            throw;
        }
    }

    /// <summary>The input types the HTML importer drops without a field (PDF-HTML-FORM-INPUTS).</summary>
    private static readonly Regex DroppedInput = new(
        """<input\b[^>]*?\stype\s*=\s*["']?(email|tel|url|time|datetime-local|month|week|color|range|file)\b""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>An HTML comment, whose inputs the importer does not see.</summary>
    private static readonly Regex HtmlComment = new("<!--.*?-->", RegexOptions.Singleline, TimeSpan.FromSeconds(1));

    /// <summary>
    /// The HTML importer keeps the name of a single-line text input only; it names every other
    /// control itself and drops its HTML value (PDF-HTML-FORM-NAMES), and it drops some input
    /// types entirely (PDF-HTML-FORM-INPUTS), which the HTML shows. Both are disclosed, so the
    /// fields are matched to their labels by position before filling.
    /// </summary>
    private static Warning? FormLosses(Document document, byte[] html)
    {
        string[] names = document.Form.Fields
            .Where(static field => field is not Aspose.Pdf.Forms.TextBoxField { Multiline: false })
            .Select(static field => $"'{field.FullName}'")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] dropped = DroppedInput.Matches(HtmlComment.Replace(Encoding.UTF8.GetString(html), string.Empty))
            .Select(static match => match.Groups[1].Value.ToLowerInvariant())
            .ToArray();
        var losses = new List<string>(2);
        if (names.Length > 0)
        {
            losses.Add($"these form fields have generated names and lost their HTML values: {string.Join(", ", names)}");
        }

        if (dropped.Length > 0)
        {
            losses.Add($"it dropped {dropped.Length} input(s) of type {string.Join(", ", dropped.Distinct(StringComparer.Ordinal))}, which have no field");
        }

        return losses.Count == 0 ? null : new Warning
        {
            Code = WarningCodes.LossyConversion,
            Message = $"The HTML importer keeps the name and value of single-line text inputs only: {string.Join("; ", losses)}.",
            Hint = "Read the fields with 'pdf query forms' and match each field's page and rect to the label beside it before filling; give a dropped input type=\"text\" to keep it as a field.",
        };
    }

    private static Document CreateFromText(string path, bool markdown, NewPdfRequest request)
    {
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw CliErrors.FileNotFound(fullPath);
        }

        (double width, double height) = PdfPageSizes.Dimensions(request.PageSize);
        ValidateMargins(request.Margins, width, height);
        var pageInfo = new PageInfo { Width = width, Height = height, Margin = Margin(request.Margins) };
        return markdown
            ? CreateFromMarkdown(fullPath, pageInfo)
            : CreateFromPlainText(fullPath, pageInfo);
    }

    /// <summary>The importer reads the whole Markdown while it constructs the document.</summary>
    private static Document CreateFromMarkdown(string path, PageInfo pageInfo)
    {
        using FileStream stream = InputFiles.OpenRead(path);
        return new Document(stream, new MdLoadOptions { PageInfo = pageInfo });
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
            using var reader = new StreamReader(InputFiles.OpenRead(path));
            while (reader.ReadLine() is { } line)
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
        if (request.InputPaths.Count < 2)
        {
            throw CliErrors.Usage(["Pass at least two PDF inputs to merge."]);
        }

        LicenseState state = _outputs.License;
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
            namedDestinations += PdfNavigationCensus.NamedDestinationNames(source).Count(name =>
                PdfNavigationCensus.Resolves(source, source.NamedDestinations[name]));
            int offset = merged.Pages.Count;
            foreach (Page page in source.Pages)
            {
                merged.Pages.Add(page);
            }

            if (request.PreserveBookmarks)
            {
                bookmarks += CopyOutline(source, source.Outlines, merged.Outlines, merged, page => offset + page);
            }

            inputs.Add(PdfInfoProjection.Source(path));
        }

        var navigation = new PdfNavigationCensus(
            bookmarks,
            Math.Max(0, PdfNavigationCensus.Unresolved(merged).Links - brokenInputLinks),
            namedDestinations);
        long size = _outputs.Write(request.Output.Path, request.Output.Overwrite, merged, merged.Save);
        List<Warning> warnings = [];
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
            Output = BuildOutput(request.Output.Path, "pdf", size),
            Inputs = inputs,
            License = EnvelopeParts.License(state),
            Warnings = warnings.Count == 0 ? null : warnings,
        };
    }

    internal PdfConvertResult Convert(string filePath, PdfConvertRequest request)
    {
        LicenseState state = _outputs.License;
        using LoadedPdf loaded = _loader.Open(filePath, request.Password);
        IReadOnlyList<int> pages = request.Pages?.Resolve(loaded.Document.Pages.Count)
            ?? Enumerable.Range(1, loaded.Document.Pages.Count).ToArray();
        List<Warning> warnings = [];
        IReadOnlyList<OutputInfo> outputs = request.Output.Format.Id switch
        {
            "png" or "jpeg" or "svg" => ConvertPages(loaded.Document, pages, request),
            "tiff" => [ConvertTiff(loaded.Document, pages, request)],
            "txt" => [ConvertText(loaded.Document, pages, request)],
            "pdfa-1b" or "pdfa-2b" or "pdfa-3b" => [ConvertPdfa(loaded.Document, pages, request, state, warnings)],
            _ => [ConvertDocument(loaded.Document, pages, request, state, warnings)],
        };

        if (request.Output.Format.Id is not ("xps" or "svg" or "png" or "jpeg" or "tiff" or "pdfa-1b" or "pdfa-2b" or "pdfa-3b"))
        {
            warnings.Add(new Warning
            {
                Code = WarningCodes.LossyConversion,
                Message = $"PDF conversion to {request.Output.Format.Id} may not preserve every layout or interactive feature.",
                Hint = request.Output.Format.Id == "html"
                    ? "Open the HTML in a browser and compare it with the PDF before relying on exact pagination, forms or annotations; review does not lay out HTML made from a PDF, so review the PDF itself."
                    : "Inspect the produced file before relying on exact pagination, forms or annotations.",
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

    /// <summary>
    /// Converts the opened document itself, which this command never saves back, so the
    /// document properties reach formats that carry them; a page copy, which evaluation mode
    /// may need, has none.
    /// </summary>
    private OutputInfo ConvertDocument(
        Document document,
        IReadOnlyList<int> pages,
        PdfConvertRequest request,
        LicenseState state,
        List<Warning> warnings)
    {
        using Document? copied = NarrowToSelection(document, pages, state, warnings, out PdfNavigationCensus degraded);
        Document selected = copied ?? document;
        if (UnselectedNavigation(degraded) is { } navigation)
        {
            warnings.Add(navigation);
        }

        SaveFormat format = request.Output.Format.Id switch
        {
            "docx" => SaveFormat.DocX,
            "xlsx" => SaveFormat.Excel,
            "pptx" => SaveFormat.Pptx,
            "html" => SaveFormat.Html,
            "epub" => SaveFormat.Epub,
            "md" => SaveFormat.Markdown,
            "xps" => SaveFormat.Xps,
            _ => throw new InvalidOperationException($"'{request.Output.Format.Id}' is not a PDF document export."),
        };
        long size = _outputs.Write(
            request.Output.Path,
            request.Output.Overwrite,
            selected,
            temp =>
            {
                if (request.Output.Format.Id == "html")
                {
                    // The HTML writer leaves <title> empty unless it is given the PDF title.
                    selected.Save(temp, new HtmlSaveOptions
                    {
                        PartsEmbeddingMode = HtmlSaveOptions.PartsEmbeddingModes.EmbedAllIntoHtml,
                        Title = selected.Info.Title ?? string.Empty,
                    });
                }
                else
                {
                    selected.Save(temp, format);
                }
            });
        return BuildOutput(request.Output.Path, request.Output.Format.Id, size);
    }

    /// <summary>
    /// Narrows the opened document to the pages --pages selected, and counts the navigation
    /// that led to the others. Evaluation mode cannot delete a page after the ones it shows,
    /// so there the selected pages of a longer document are copied into a new document, which
    /// is returned, and what the copy leaves behind is disclosed.
    /// </summary>
    private static Document? NarrowToSelection(
        Document document,
        IReadOnlyList<int> pages,
        LicenseState state,
        List<Warning> warnings,
        out PdfNavigationCensus degraded)
    {
        if (state == LicenseState.Evaluation
            && document.Pages.Count > PdfEvaluation.VisiblePages
            && pages.Count < document.Pages.Count)
        {
            degraded = default;
            warnings.Add(new Warning
            {
                Code = WarningCodes.LossyConversion,
                Message = $"Evaluation mode cannot remove the pages after page {PdfEvaluation.VisiblePages} from the document, so the selected pages were copied into a new one, which has none of the document's bookmarks, attachments and document properties, such as its title, author or subject.",
                Hint = "Apply an Aspose.PDF license to keep them.",
            });
            return Select(document, pages);
        }

        degraded = DeleteUnselected(document, pages);
        return null;
    }

    /// <summary>
    /// Deletes the pages --pages did not select from the opened document, and counts the
    /// navigation that led to them.
    /// </summary>
    private static PdfNavigationCensus DeleteUnselected(Document document, IReadOnlyList<int> pages)
    {
        int[] excluded = Enumerable.Range(1, document.Pages.Count).Except(pages).ToArray();
        if (excluded.Length == 0)
        {
            return default;
        }

        PdfNavigationCensus before = PdfNavigationCensus.Unresolved(document);
        document.Pages.Delete(excluded);
        return PdfNavigationCensus.Degraded(before, PdfNavigationCensus.Unresolved(document));
    }

    private static Warning? UnselectedNavigation(PdfNavigationCensus degraded) => degraded.ToWarning(
        "lead to pages that --pages did not select",
        "Select every page the navigation needs, or delete those bookmarks and links in a 'pdf edit' batch before converting.");

    private OutputInfo ConvertText(Document source, IReadOnlyList<int> pages, PdfConvertRequest request)
    {
        long size = _outputs.Write(request.Output.Path, request.Output.Overwrite, source, temp =>
            File.WriteAllText(temp, DocumentText(source, pages), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)), rendering: true, pages: pages);
        return BuildOutput(request.Output.Path, "txt", size);
    }

    /// <summary>
    /// Converts the opened document itself, which this command never saves back, so the
    /// outline, attachments, metadata and page labels reach the archive; a page copy carries
    /// none of them, which evaluation mode discloses when it has to copy pages. The engine
    /// removes what the profile does not allow, and each removed attachment or bookmark is
    /// reported on its own.
    /// </summary>
    private OutputInfo ConvertPdfa(
        Document opened,
        IReadOnlyList<int> pages,
        PdfConvertRequest request,
        LicenseState state,
        List<Warning> warnings)
    {
        string profile = request.Output.Format.Id;
        PdfFormat format = profile switch
        {
            "pdfa-1b" => PdfFormat.PDF_A_1B,
            "pdfa-2b" => PdfFormat.PDF_A_2B,
            "pdfa-3b" => PdfFormat.PDF_A_3B,
            _ => throw new InvalidOperationException($"'{profile}' is not a PDF/A profile."),
        };

        // Navigation is counted around the page deletion alone; a bookmark the conversion
        // removes is reported with the outline below.
        using Document? copied = NarrowToSelection(opened, pages, state, warnings, out PdfNavigationCensus degraded);
        Document document = copied ?? opened;

        // PDF/A forbids encryption; the engine cannot convert an encrypted document.
        if (document.IsEncrypted)
        {
            document.Decrypt();
        }

        string[] attachments = AttachmentNames(document);
        FileSpecification[] untyped = document.EmbeddedFiles
            .Where(static file => string.IsNullOrEmpty(file.MIMEType))
            .ToArray();
        int bookmarks = Editing.PdfMutationSupport.CountOutline(document.Outlines);
        int changed = 0;
        long size = _outputs.Write(request.Output.Path, request.Output.Overwrite, document, temp =>
        {
            using var log = new MemoryStream();
            PdfComplianceLog.EnsureConverted(
                document.Convert(log, format, ConvertErrorAction.Delete), log, profile);
            // The identification it writes and the attachments, reported below, are not counted.
            changed = PdfComplianceLog.Parse(log)
                .Count(static problem => problem.Section is not ("Metadata" or "EmbeddedFiles"));
            // Only PDF/A-3 keeps attachments that are not PDF documents.
            if (format == PdfFormat.PDF_A_3B)
            {
                LabelUntypedAttachments(document, untyped);
            }

            document.Save(temp);
            using LoadedPdf saved = _loader.OpenPublishedCandidate(temp, password: null);
            using var validation = new MemoryStream();
            PdfComplianceLog.EnsureConformant(
                saved.Document.Validate(validation, format), validation, profile, page => ButtonFields(saved.Document, page));
        });

        foreach (string removed in attachments.Except(AttachmentNames(document), StringComparer.Ordinal))
        {
            warnings.Add(new Warning
            {
                Code = WarningCodes.LossyConversion,
                Location = $"attachment {removed}",
                Message = $"Attachment '{removed}' was removed: {AttachmentRule(profile)}.",
                Hint = profile == "pdfa-3b"
                    ? "Deliver the file alongside the archive."
                    : "Convert to pdfa-3b to keep attachments, or deliver the file alongside the archive.",
            });
        }

        int lostBookmarks = bookmarks - Editing.PdfMutationSupport.CountOutline(document.Outlines);
        if (lostBookmarks > 0)
        {
            warnings.Add(new Warning
            {
                Code = WarningCodes.LossyConversion,
                Location = "outline",
                Message = $"{lostBookmarks} bookmark(s) were removed by the conversion to {profile}.",
                Hint = "Compare 'pdf inspect --detail outline' of both files and re-create the missing bookmarks with add_bookmark.",
            });
        }

        if (UnselectedNavigation(degraded) is { } navigation)
        {
            warnings.Add(navigation);
        }

        if (changed > 0)
        {
            warnings.Add(new Warning
            {
                Code = WarningCodes.LossyConversion,
                Message = $"Conversion to {profile} changed {changed} item(s) the profile does not allow, such as fonts that were not embedded, transparency, actions or prohibited annotation entries.",
                Hint = $"Run 'pdf validate <original> --profile {profile}' to list them, and compare the pages of both files.",
            });
        }

        return BuildOutput(request.Output.Path, profile, size);
    }

    /// <summary>
    /// The full names of the check boxes and radio groups on a page whose normal appearance is
    /// one stream rather than one per state, which clause 6.3.3 rejects; the form lists a radio
    /// group as its buttons. The SDK names an appearance per state <c>N.&lt;state&gt;</c>.
    /// </summary>
    private static IEnumerable<string> ButtonFields(Document document, int page) => document.Form.Fields
        .Where(field => field is (Aspose.Pdf.Forms.CheckboxField or Aspose.Pdf.Forms.RadioButtonField or Aspose.Pdf.Forms.RadioButtonOptionField)
            && field.PageIndex == page
            && (PdfFormService.RadioGroup(field) ?? field).Appearance.Keys.Contains("N"))
        .Select(static field => field.FullName);

    /// <summary>
    /// The conversion labels every attachment that had no media type <c>application/pdf</c>
    /// (PDF-PDFA-ATTACHMENT-TYPE); one that is not a PDF is labelled
    /// <c>application/octet-stream</c>, the type of unidentified data, instead.
    /// </summary>
    private static void LabelUntypedAttachments(Document document, IReadOnlyCollection<FileSpecification> untyped)
    {
        foreach (FileSpecification file in document.EmbeddedFiles)
        {
            if (untyped.Contains(file) && !StartsAsPdf(file))
            {
                file.MIMEType = "application/octet-stream";
            }
        }
    }

    private static bool StartsAsPdf(FileSpecification file)
    {
        Stream? contents = file.Contents;
        if (contents is null)
        {
            return false;
        }

        if (contents.CanSeek)
        {
            contents.Position = 0;
        }

        return ContainerSignatures.HasPdfHeader(ContainerSignatures.ReadPrefix(contents));
    }

    private static string[] AttachmentNames(Document document) =>
        document.EmbeddedFiles
            .Select(static file => file.UnicodeName ?? file.Name)
            .Where(static name => !string.IsNullOrEmpty(name))
            .ToArray();

    /// <summary>Why the profile does not keep an attachment.</summary>
    private static string AttachmentRule(string profile) => profile switch
    {
        "pdfa-1b" => "PDF/A-1 does not allow attachments",
        "pdfa-2b" => "PDF/A-2 allows only attachments that are PDF/A documents",
        _ => $"the engine could not make it conform to {profile}",
    };

    private IReadOnlyList<OutputInfo> ConvertPages(
        Document document,
        IReadOnlyList<int> pages,
        PdfConvertRequest request)
    {
        string outputDirectory = request.Output.Directory;
        using OutputSet<Document> writer = _outputs.BeginSet([outputDirectory], "pdf-convert");
        var paths = new List<string>(pages.Count);
        foreach (int pageNumber in pages)
        {
            string path = request.Output.Part(PagePart, pageNumber, pages.Count);
            writer.Stage(path, request.Output.Overwrite, document, temp =>
            {
                using FileStream stream = File.Create(temp);
                RenderPage(document, pageNumber, request.Output.Format.Id, ImageDpi, stream);
            }, rendering: true, pages: [pageNumber]);
            paths.Add(path);
        }

        IReadOnlyList<long> sizes = writer.Commit();
        return paths.Select((path, index) =>
            BuildOutput(path, request.Output.Format.Id, sizes[index])).ToArray();
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
        long size = _outputs.Write(request.Output.Path, request.Output.Overwrite, selected, temp =>
        {
            using FileStream stream = File.Create(temp);
            var device = new TiffDevice(new Resolution(ImageDpi), new TiffSettings());
            device.Process(selected, 1, selected.Pages.Count, stream);
        }, rendering: true);
        return BuildOutput(request.Output.Path, "tiff", size);
    }

    private static MarginInfo Margin(PdfMargins margins) => new()
    {
        Top = margins.Top,
        Right = margins.Right,
        Bottom = margins.Bottom,
        Left = margins.Left,
    };

    /// <summary>Refuses margins that leave no content area on a page, named by <paramref name="page"/> when given.</summary>
    private static void ValidateMargins(PdfMargins margins, double width, double height, string? page = null)
    {
        if (margins.Top < 0 || margins.Right < 0 || margins.Bottom < 0 || margins.Left < 0
            || margins.Left + margins.Right >= width
            || margins.Top + margins.Bottom >= height)
        {
            throw CliErrors.OptionInvalid(
                "--margins",
                page is null
                    ? "values must be non-negative and leave a positive content area"
                    : $"values must be non-negative and leave a positive content area on {page}",
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

    /// <summary>Checks every creation input and returns the HTML input's bytes, if any.</summary>
    private static byte[]? EnsureCreationInputs(
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

        foreach (string image in request.ImagePaths ?? [])
        {
            using Stream content = resourceBudgets.Inputs.OpenFile(image);
            NetworkReferenceGuard.EnsureNoneInImage(content, image);
        }

        if (request.HtmlPath is not { } html)
        {
            return null;
        }

        // The HTML importer reaches the network before any resource policy applies; refuse
        // first unless the caller allowed it. Markdown is checked with its local references.
        byte[] bytes = resourceBudgets.Inputs.ReadAllBytes(html);
        if (!request.AllowNetworkResources)
        {
            NetworkReferenceGuard.EnsureNone(bytes, "HTML input", html, optIn: "--allow-network-resources");
        }

        return bytes;
    }

}
