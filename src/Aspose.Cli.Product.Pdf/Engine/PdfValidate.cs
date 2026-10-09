using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Validates a PDF against one PDF/A profile: <c>pdf validate</c>.</summary>
internal static class PdfValidate
{
    /// <summary>The most validation issues one result lists.</summary>
    private const int ListedIssues = 100;

    internal static PdfValidateResult Run(PdfSession session, PdfValidateRequest request)
    {
        string filePath = request.Input;
        PdfFormat format = request.Profile.ToLowerInvariant() switch
        {
            "pdfa-1b" => PdfFormat.PDF_A_1B,
            "pdfa-2b" => PdfFormat.PDF_A_2B,
            "pdfa-3b" => PdfFormat.PDF_A_3B,
            _ => throw CliErrors.OptionInvalid("--profile", $"unknown profile '{request.Profile}'", "Use pdfa-1b, pdfa-2b or pdfa-3b."),
        };
        LicenseState state = session.Outputs.License;
        using LoadedPdf loaded = session.Loader.Open(filePath, request.Password);
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
