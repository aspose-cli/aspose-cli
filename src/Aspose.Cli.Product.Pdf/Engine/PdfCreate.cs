using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// <c>pdf create</c>: the engine's new document, new Document(), which has no page, or a template
/// saved as a new PDF; with --encrypt, encrypted as edit encrypts an output.
/// </summary>
internal static class PdfCreate
{
    internal static PdfCreateResult Run(PdfSession session, PdfCreateRequest request)
    {
        // The license is applied before the engine creates or opens anything.
        LicenseState state = session.Outputs.License;
        // A template opens without a password: an encrypted one is refused.
        using LoadedPdf? template = request.Template is null ? null : session.Loader.Open(request.Template.Path, PasswordSource.None());
        using Document? created = template is null ? new Document() : null;
        Document document = template?.Document ?? created!;
        OutputProtection protection = OutputProtection.For(request.Output.Format, request.EncryptPassword, new InputProtection(Encrypted: false), inputPassword: null);
        var documents = new PdfDocuments(session.Loader);
        PublishedFile written = session.Outputs.Write(request.Output.Path, request.Output.Format, request.Output.Overwrite, temp =>
            documents.Save(document, temp, protection), encrypted: protection.Encrypted);
        return new PdfCreateResult
        {
            Output = FileObjects.Output(written, request.Output.Format, protection.Encrypted),
            Inputs = template is null ? [] : FileObjects.Sources((request.Template!, template.FormatId, template.Protection.Encrypted)),
            License = EnvelopeParts.License(state),
        };
    }
}
