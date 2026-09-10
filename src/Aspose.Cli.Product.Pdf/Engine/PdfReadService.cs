using System.Text;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Product.Pdf.Engine.Mapping;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;
using Aspose.Pdf.Devices;
using Aspose.Pdf.Text;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Product-internal Aspose.PDF operations used by focused workflows.</summary>
internal sealed class PdfReadService
{
    private const int ImageDpi = 192;
    private readonly ILicenseGate _licenseGate;
    private readonly SafeFileWriter _writer;
    private readonly PdfDocumentLoader _loader;

    internal PdfReadService(
        ILicenseGate licenseGate,
        SafeFileWriter writer,
        PdfDocumentLoader loader)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _loader = loader;
    }

    /// <inheritdoc />
    internal PdfInfoResult GetInfo(string filePath, PdfInfoRequest request) =>
        PdfErrorTranslator.Execute("inspect", () => GetInfoCore(filePath, request));

    /// <inheritdoc />
    internal PdfReadResult Read(string filePath, PdfReadRequest request) =>
        PdfErrorTranslator.Execute("query pages", () => ReadCore(filePath, request));

    /// <inheritdoc />
    internal PdfConvertResult Convert(string filePath, PdfConvertRequest request) =>
        PdfErrorTranslator.Execute("convert", () => ConvertCore(filePath, request));

    private PdfInfoResult GetInfoCore(string filePath, PdfInfoRequest request)
    {
        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedPdf loaded = _loader.Open(filePath, request.Password);
        return PdfInfoProjection.Project(loaded, filePath, request) with
        {
            License = EnvelopeParts.License(state),
        };
    }

    private PdfReadResult ReadCore(string filePath, PdfReadRequest request)
    {
        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedPdf loaded = _loader.Open(filePath, request.Password);
        IReadOnlyList<int> requested = request.Pages?.Resolve(loaded.Document.Pages.Count)
            ?? Enumerable.Range(1, loaded.Document.Pages.Count).ToArray();
        var pages = new List<PdfPageText>();
        var scanned = new List<int>();
        int remaining = request.MaxCharacters;
        int consumed = 0;

        foreach (int pageNumber in requested)
        {
            if (remaining == 0)
            {
                break;
            }

            Page page = loaded.Document.Pages[pageNumber];
            string text = ExtractText(page, request.Mode);
            bool truncated = text.Length > remaining;
            string projected = truncated ? text[..remaining] : text;
            pages.Add(new PdfPageText
            {
                Number = pageNumber,
                Text = projected,
                Truncated = truncated,
            });
            consumed++;
            remaining -= projected.Length;

            if (string.IsNullOrWhiteSpace(text) && IsImageDominated(page))
            {
                scanned.Add(pageNumber);
            }
        }

        bool windowTruncated = consumed < requested.Count || pages.Any(static page => page.Truncated);
        string? next = NextCommand(filePath, request, requested, consumed, windowTruncated);
        IReadOnlyList<Warning>? warnings = scanned.Count == 0
            ? null
            : [new Warning
            {
                Code = PdfDiagnostics.ScannedPagesSuspected,
                Message = $"Pages with no extractable text appear image-dominated: {string.Join(", ", scanned)}.",
                Hint = "Use 'aspose-cli ocr recognize' when the OCR product is available, or inspect rendered pages.",
            }];

        return new PdfReadResult
        {
            Source = PdfInfoProjection.Source(filePath),
            Mode = request.Mode,
            Window = new PdfPageWindow
            {
                Pages = pages.Count == 0
                    ? string.Empty
                    : PageRangeText(pages.Select(static page => page.Number)),
                Of = loaded.Document.Pages.Count,
                Truncated = windowTruncated,
            },
            Pages = pages,
            ScannedPagesSuspected = scanned.Count == 0 ? null : scanned,
            Next = next,
            License = EnvelopeParts.License(state),
            Warnings = warnings,
        };
    }

    private PdfConvertResult ConvertCore(string filePath, PdfConvertRequest request)
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
