using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;
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
        PdfInfoResult info = PdfInfoProjection.Project(loaded, filePath, request);
        return info with
        {
            License = EnvelopeParts.License(state),
            Warnings = PdfEvaluation.InputTruncated(state, loaded.Document.Pages.Count) is { } truncated
                ? EnvelopeParts.CombineWarnings(info.Warnings, [truncated])
                : info.Warnings,
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
                Page = pageNumber,
                Text = projected,
                Truncated = truncated,
            });
            remaining -= projected.Length;

            if (HasNoOwnText(text) && IsImageDominated(page))
            {
                scanned.Add(pageNumber);
            }
        }

        bool windowTruncated = pages.Count < requested.Count || pages.Any(static page => page.Truncated);
        IReadOnlyList<Warning>? warnings = scanned.Count == 0
            ? null
            : [new Warning
            {
                Code = PdfDiagnostics.ScannedPagesSuspected,
                Message = $"Pages with no extractable text appear image-dominated: {string.Join(", ", scanned)}.",
                Hint = "This build has no OCR: read those pages from rendered images ('aspose-cli pdf render'), and hide content "
                    + "on them with redact_area, whose coordinates you can read from 'aspose-cli pdf render --grid 50', "
                    + "which labels points from the page's top-left corner.",
            }];

        return new PdfReadResult
        {
            Source = PdfInfoProjection.Source(filePath),
            Mode = request.Mode,
            PageCount = loaded.Document.Pages.Count,
            Pages = pages,
            Window = new ResultWindow
            {
                Unit = "page",
                Returned = pages.Count,
                Total = requested.Count,
                Truncated = windowTruncated,
            },
            ScannedPagesSuspected = scanned.Count == 0 ? null : scanned,
            License = EnvelopeParts.License(state),
            Warnings = warnings,
        };
    }

    /// <summary>
    /// Whether the page's text is at most the notice an evaluation-mode save stamps on it, so a
    /// scan saved without a license still reads as a scan.
    /// </summary>
    private static bool HasNoOwnText(string text) =>
        PdfEvaluation.Notice.Replace(text, string.Empty).All(static character => char.IsWhiteSpace(character) || character == '.');
}
