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
        int count = Math.Min(total, request.MaxPartCount);
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
            TotalPartCount = total,
            Parts = parts,
        };
    }

    internal PdfRenderResult Render(string filePath, PdfRenderRequest request)
    {
        if (!PdfFormats.IsRender(request.TargetFormatId))
        {
            throw CliErrors.FormatUnsupported(request.TargetFormatId, PdfFormats.RenderIds);
        }

        PdfRenderGrid? grid = RenderGrid(request);
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
                    stream,
                    grid);
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
            Grid = grid,
            License = EnvelopeParts.License(state),
            Warnings = EnvelopeParts.OutputWarnings(state),
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
                throw CliErrors.FormatUnsupported(format, PdfFormats.RenderIds);
        }
    }

    /// <summary>Validates the requested grid, which only raster output carries.</summary>
    private static PdfRenderGrid? RenderGrid(PdfRenderRequest request)
    {
        if (request.Grid is not int spacing)
        {
            return null;
        }

        if (request.TargetFormatId == "svg")
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
        using MarkdownImportResources? markdown = request.Markdown && request.TextPath is { } markdownPath
            ? new MarkdownImportResources(markdownPath, _resourceBudgets, Environment.CurrentDirectory) : null;
        LicenseState state = _licenseGate.EnsureApplied();
        using HtmlImportResources? resources = request.HtmlPath is { } htmlPath
            ? new HtmlImportResources(htmlPath, _resourceBudgets, request.AllowNetworkResources) : null;
        using Document document = request.ImagePaths is { Count: > 0 } images
            ? CreateFromImages(images, request)
            : request.HtmlPath is not null
                ? CreateFromHtml(request.HtmlPath, request, resources!)
                : CreateFromText(request.TextPath!, request.Markdown, request);
        resources?.ThrowIfFailed();
        if (request.HtmlPath is not null || request.Markdown)
        {
            // Both importers set the title, author and subject to a placeholder
            // (PDF-IMPORT-INFO-PLACEHOLDER); keep only the title the HTML states.
            document.Info.Title = html is null ? string.Empty : HtmlDocumentTitle.Read(html) ?? string.Empty;
            document.Info.Author = string.Empty;
            document.Info.Subject = string.Empty;
        }

        long size = _writer.Write(request.OutputPath, request.Overwrite, path =>
        {
            try { document.Save(path); }
            finally { resources?.ThrowIfFailed(); }
        });
        var warnings = EnvelopeParts.OutputWarnings(state)?.ToList() ?? [];
        warnings.AddRange(resources?.Warnings ?? []);

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
            (double width, double height) = PdfPageSizes.Dimensions(request.PageSize);
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

        (double width, double height) = PdfPageSizes.Dimensions(request.PageSize);
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
        List<Warning> warnings = [.. EnvelopeParts.OutputWarnings(state) ?? []];
        IReadOnlyList<OutputInfo> outputs = request.TargetFormatId switch
        {
            "png" or "jpeg" or "svg" => ConvertPages(loaded.Document, pages, request),
            "tiff" => [ConvertTiff(loaded.Document, pages, request)],
            "txt" => [ConvertText(loaded.Document, pages, request)],
            "pdfa-1b" or "pdfa-2b" or "pdfa-3b" => [ConvertPdfa(loaded.Document, pages, request, state, warnings)],
            _ => [ConvertDocument(loaded.Document, pages, request, state, warnings)],
        };

        if (request.TargetFormatId is not ("xps" or "svg" or "png" or "jpeg" or "tiff" or "pdfa-1b" or "pdfa-2b" or "pdfa-3b"))
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
        long size = _writer.Write(request.OutputPath, request.Overwrite, temp =>
            File.WriteAllText(temp, DocumentText(source, pages), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)));
        return BuildOutput(request.OutputPath, "txt", size);
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
        string profile = request.TargetFormatId;
        PdfFormat format = profile switch
        {
            "pdfa-1b" => PdfFormat.PDF_A_1B,
            "pdfa-2b" => PdfFormat.PDF_A_2B,
            "pdfa-3b" => PdfFormat.PDF_A_3B,
            _ => throw CliErrors.FormatUnsupported(profile, PdfFormats.PdfaConvertIds),
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
        long size = _writer.Write(request.OutputPath, request.Overwrite, temp =>
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

        return BuildOutput(request.OutputPath, profile, size);
    }

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

        Span<byte> head = stackalloc byte[1024];
        int read = contents.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
        if (contents.CanSeek)
        {
            contents.Position = 0;
        }

        return head[..read].IndexOf("%PDF-"u8) >= 0;
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
