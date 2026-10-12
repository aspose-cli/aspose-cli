using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Projects PDF structure, security state and metadata: <c>pdf inspect</c>.</summary>
internal static class PdfInfo
{
    internal static PdfInspectResult Run(PdfSession session, PdfInspectRequest request)
    {
        string filePath = request.Input;
        LicenseState state = session.Outputs.License;
        using LoadedPdf loaded = session.Loader.Open(filePath, request.Password);
        // Page sizes and page content describe only the pages evaluation mode shows.
        session.Outputs.ReadInput(loaded.Document);
        PdfInspectResult info = PdfInfoProjection.Project(loaded, filePath, request);
        return info with { License = EnvelopeParts.License(state) };
    }
}
