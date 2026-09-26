using System.Globalization;
using System.Text.RegularExpressions;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Product.Pdf.Engine.Mapping;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Text;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Devices;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Optimization;
using Aspose.Pdf.Text;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;
using PdfColor = Aspose.Pdf.Color;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Owns bounded content inspection and standards validation.</summary>
internal sealed class PdfInspectionService
{
    /// <summary>The most validation issues one result lists.</summary>
    private const int ListedIssues = 100;

    private readonly ILicenseGate _licenseGate;
    private readonly PdfDocumentLoader _loader;

    internal PdfInspectionService(
        ILicenseGate licenseGate,
        PdfDocumentLoader loader)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        _loader = loader;
    }

    public PdfSearchResult Search(string filePath, PdfSearchRequest request)
    {
        TextSearch text = request.Query.Text;
        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedPdf loaded = _loader.Open(filePath, request.Password);
        IReadOnlyList<int> pages = request.Pages?.Resolve(loaded.Document.Pages.Count)
            ?? Enumerable.Range(1, loaded.Document.Pages.Count).ToArray();
        SearchHits<PdfSearchHit> hits = request.Query.Collect<PdfSearchHit>();
        foreach (int number in pages)
        {
            Page page = loaded.Document.Pages[number];
            int occurrence = 0;
            foreach (TextFragment fragment in MatchText(page, text.Pattern, text.Expression is not null, text.CaseSensitive,
                static reason => CliErrors.OptionInvalid("--pattern", reason, "Use a pattern that matches at least one character.")))
            {
                int current = ++occurrence;
                if (!hits.Offer(() => new PdfSearchHit
                {
                    Page = number,
                    Snippet = fragment.Text,
                    Rect = ToContractRect(page, fragment.Rectangle),
                    Occurrence = current,
                }))
                {
                    break;
                }
            }

            if (hits.Truncated)
            {
                break;
            }
        }

        return new PdfSearchResult
        {
            Source = PdfInfoProjection.Source(filePath),
            Pattern = text.Pattern,
            Hits = hits.Hits,
            Window = hits.Window(),
            License = EnvelopeParts.License(state),
        };
    }

    public PdfValidateResult Validate(string filePath, PdfValidateRequest request)
    {
        PdfFormat format = request.Profile.ToLowerInvariant() switch
        {
            "pdfa-1b" => PdfFormat.PDF_A_1B,
            "pdfa-2b" => PdfFormat.PDF_A_2B,
            "pdfa-3b" => PdfFormat.PDF_A_3B,
            _ => throw CliErrors.OptionInvalid("--profile", $"unknown profile '{request.Profile}'", "Use pdfa-1b, pdfa-2b or pdfa-3b."),
        };
        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedPdf loaded = _loader.Open(filePath, request.Password);
        using var log = new MemoryStream();
        bool valid = loaded.Document.Validate(log, format);
        IReadOnlyList<PdfComplianceProblem> problems = PdfComplianceLog.Parse(log);
        return new PdfValidateResult
        {
            Input = PdfInfoProjection.Source(filePath),
            Profile = request.Profile.ToLowerInvariant(),
            Valid = valid,
            Issues = problems.Take(ListedIssues).Select(static problem => problem.ToString()).ToArray(),
            License = EnvelopeParts.License(state),
            Warnings = problems.Count > ListedIssues
                ? [EnvelopeParts.ListTruncated(
                    "issues",
                    ListedIssues,
                    problems.Count,
                    "Fix the listed issues and validate again; 'pdf convert --to <profile>' fixes the ones it can.")]
                : null,
        };
    }
}
