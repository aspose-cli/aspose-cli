using System.Diagnostics;
using System.Text;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary><c>pdf validate</c>: Document.Validate(log, format), its return value and its log as the engine wrote it.</summary>
internal static class PdfValidate
{
    internal static PdfValidateResult Run(PdfSession session, PdfValidateRequest request)
    {
        PdfFormat format = PdfEngineFormats.Archive(request.Profile.ToLowerInvariant())
            ?? throw new UnreachableException($"--profile {request.Profile} reached the engine, which ValidateCommand refuses.");
        LicenseState state = session.Outputs.License;
        using LoadedPdf loaded = session.Loader.Open(request.Input, request.Password);
        using var log = new MemoryStream();
        bool valid = loaded.Document.Validate(log, format);
        return new PdfValidateResult
        {
            Input = FileObjects.Input(request.Input, loaded.FormatId, loaded.Protection.Encrypted),
            Profile = request.Profile.ToLowerInvariant(),
            Valid = valid,
            Log = Encoding.UTF8.GetString(log.ToArray()),
            License = EnvelopeParts.License(state),
        };
    }
}
