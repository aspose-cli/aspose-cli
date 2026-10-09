using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Projects PDF structure, security state and metadata: <c>pdf inspect</c>.</summary>
internal static class PdfInfo
{
    internal static PdfInfoResult Run(PdfSession session, PdfInfoRequest request)
    {
        string filePath = request.Input;
        LicenseState state = session.Outputs.License;
        using LoadedPdf loaded = session.Loader.Open(filePath, request.Password);
        PdfInfoResult info = PdfInfoProjection.Project(loaded, filePath, request);
        return info with
        {
            License = EnvelopeParts.License(state),
            Warnings = PdfEvaluation.InputTruncated(state, loaded.Document.Pages.Count) is { } truncated
                ? EnvelopeParts.CombineWarnings(info.Warnings, [truncated])
                : info.Warnings,
        };
    }
}
