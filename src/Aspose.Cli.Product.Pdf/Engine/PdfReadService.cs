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

/// <summary>Owns PDF structure and bounded content reading.</summary>
internal sealed class PdfReadService
{
    private readonly ILicenseGate _licenseGate;
    private readonly PdfDocumentLoader _loader;

    internal PdfReadService(
        ILicenseGate licenseGate,
        PdfDocumentLoader loader)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        _loader = loader;
    }

    internal PdfInfoResult GetInfo(string filePath, PdfInfoRequest request)
    {
        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedPdf loaded = _loader.Open(filePath, request.Password);
        return PdfInfoProjection.Project(loaded, filePath, request) with
        {
            License = EnvelopeParts.License(state),
        };
    }

    internal PdfReadResult Read(string filePath, PdfReadRequest request)
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
            License = EnvelopeParts.License(state),
            Warnings = warnings,
        };
    }

}
