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

using static Aspose.Cli.Product.Pdf.Engine.PdfArtifactSupport;

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
            RenderPixelGuard.EnsureFits(
                (long)Math.Ceiling(page.Rect.Width / 72d * dpi),
                (long)Math.Ceiling(page.Rect.Height / 72d * dpi),
                dpi);
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
        Document document = markdown
            ? new Document(fullPath, new MdLoadOptions
            {
                PageInfo = new PageInfo
                {
                    Width = width,
                    Height = height,
                    Margin = Margin(request.Margins),
                },
            })
            : new Document(fullPath, new TxtLoadOptions());
        foreach (Page page in document.Pages)
        {
            page.SetPageSize(width, height);
            page.PageInfo.Margin = Margin(request.Margins);
        }

        return document;
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

        var warnings = new List<Warning>();
        if (state == LicenseState.Evaluation)
        {
            warnings.Add(EnvelopeParts.EvaluationWatermark);
        }

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
        {
            var builder = new StringBuilder();
            foreach (int pageNumber in pages)
            {
                if (builder.Length > 0)
                {
                    builder.Append('\f').AppendLine();
                }

                builder.Append(ExtractText(source.Pages[pageNumber], PdfReadModes.Plain));
            }

            File.WriteAllText(temp, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        });
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
            string log = temp + ".conversion.xml";
            try
            {
                _ = selected.Convert(log, format, ConvertErrorAction.Delete);
                selected.Save(temp);
            }
            finally
            {
                if (File.Exists(log))
                {
                    File.Delete(log);
                }
            }
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
            string path = pages.Count == 1 ? request.OutputPath : PagePath(request.OutputPath, pageNumber);
            writer.Stage(path, request.Overwrite, temp =>
            {
                using FileStream stream = File.Create(temp);
                Page page = document.Pages[pageNumber];
                switch (request.TargetFormatId)
                {
                    case "png":
                        new PngDevice(new Resolution(ImageDpi)).Process(page, stream);
                        break;
                    case "jpeg":
                        new JpegDevice(new Resolution(ImageDpi), 95).Process(page, stream);
                        break;
                    case "svg":
                        using (Document selected = Select(document, [pageNumber]))
                        {
                            selected.Save(stream, SaveFormat.Svg);
                        }
                        break;
                    default:
                        throw CliErrors.FormatUnsupported(request.TargetFormatId, PdfFormats.ImageConvertIds);
                }
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
        using Document selected = Select(source, pages);
        long size = _writer.Write(request.OutputPath, request.Overwrite, temp =>
        {
            using FileStream stream = File.Create(temp);
            var device = new TiffDevice(new Resolution(ImageDpi), new TiffSettings());
            device.Process(selected, 1, selected.Pages.Count, stream);
        });
        return BuildOutput(request.OutputPath, "tiff", size);
    }
}
