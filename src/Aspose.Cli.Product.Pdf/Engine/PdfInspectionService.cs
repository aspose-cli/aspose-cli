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
using static Aspose.Cli.Product.Pdf.Engine.PdfArtifactSupport;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;
using static Aspose.Cli.Product.Pdf.Engine.PdfMutationSupport;
using PdfColor = Aspose.Pdf.Color;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Owns bounded content inspection and standards validation.</summary>
internal sealed class PdfInspectionService
{
    private readonly ILicenseGate _licenseGate;
    private readonly PdfDocumentLoader _loader;

    internal PdfInspectionService(
        ILicenseGate licenseGate,
        PdfDocumentLoader loader)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        _loader = loader;
    }

    public PdfSearchResult Search(string filePath, PdfSearchRequest request) =>
        PdfErrorTranslator.Execute("query search", () => SearchCore(filePath, request));

    /// <inheritdoc />
    public PdfValidateResult Validate(string filePath, PdfValidateRequest request) =>
        PdfErrorTranslator.Execute("validate", () => ValidateCore(filePath, request));

    private PdfSearchResult SearchCore(string filePath, PdfSearchRequest request)
    {
        if (request.MaxHits < 1 || request.MaxHits > 10_000)
        {
            throw CliErrors.OptionInvalid("--max-hits", "must be from 1 through 10000", "Choose a bounded positive hit count.");
        }

        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedPdf loaded = _loader.Open(filePath, request.Password);
        IReadOnlyList<int> pages = request.Pages?.Resolve(loaded.Document.Pages.Count)
            ?? Enumerable.Range(1, loaded.Document.Pages.Count).ToArray();
        if (request.Regex)
        {
            try
            {
                _ = SafeRegex.Create(request.Pattern, request.CaseSensitive);
            }
            catch (ArgumentException exception)
            {
                throw CliErrors.OptionInvalid("--pattern", exception.Message, "Fix the regular expression syntax.");
            }
        }
        var hits = new List<PdfSearchHit>();
        bool truncated = false;
        foreach (int number in pages)
        {
            Page page = loaded.Document.Pages[number];
            IReadOnlyList<string> phrases = MatchPhrases(
                page,
                request.Pattern,
                request.Regex,
                request.CaseSensitive);
            int occurrence = 0;
            foreach (string phrase in phrases)
            {
                string expression = Regex.Escape(phrase);
                if (!request.CaseSensitive && !request.Regex)
                {
                    expression = "(?i:" + expression + ")";
                }

                var absorber = new TextFragmentAbsorber(expression)
                {
                    TextSearchOptions = new TextSearchOptions(isRegularExpressionUsed: true),
                };
                page.Accept(absorber);
                foreach (TextFragment fragment in absorber.TextFragments)
                {
                    occurrence++;
                    if (hits.Count == request.MaxHits)
                    {
                        truncated = true;
                        break;
                    }

                    hits.Add(new PdfSearchHit
                    {
                        Page = number,
                        Snippet = fragment.Text,
                        Rect = ToContractRect(page, fragment.Rectangle),
                        Occurrence = occurrence,
                    });
                }

                if (truncated)
                {
                    break;
                }
            }

            if (truncated)
            {
                break;
            }
        }

        return new PdfSearchResult
        {
            Input = PdfInfoProjection.Source(filePath),
            Pattern = request.Pattern,
            Hits = hits,
            Truncated = truncated,
            License = EnvelopeParts.License(state),
        };
    }

    private PdfValidateResult ValidateCore(string filePath, PdfValidateRequest request)
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
        string log = Path.Combine(Path.GetTempPath(), $"aspose-cli-pdf-validate-{Guid.NewGuid():N}.xml");
        try
        {
            bool valid = loaded.Document.Validate(log, format);
            string[] issues = File.Exists(log)
                ? File.ReadLines(log)
                    .Select(static line => line.Trim())
                    .Where(static line => line.Contains("<Problem", StringComparison.OrdinalIgnoreCase)
                        || line.Contains("<Error", StringComparison.OrdinalIgnoreCase))
                    .Take(101)
                    .ToArray()
                : [];
            return new PdfValidateResult
            {
                Input = PdfInfoProjection.Source(filePath),
                Profile = request.Profile.ToLowerInvariant(),
                Valid = valid,
                Issues = issues.Take(100).ToArray(),
                Truncated = issues.Length > 100,
                License = EnvelopeParts.License(state),
            };
        }
        finally
        {
            if (File.Exists(log))
            {
                File.Delete(log);
            }
        }
    }
}
