using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class PdfHelpMetadata
{
    public static void Attach(Command root)
    {
        root.WithExamples(
            [
                "aspose-cli pdf inspect report.pdf --preview --detail permissions forms signatures",
                "aspose-cli pdf query pages report.pdf --pages 1-5 --mode layout --output json",
                "aspose-cli pdf edit report.pdf --ops ops.json --out reviewed.pdf",
                "aspose-cli pdf sign reviewed.pdf --certificate signer.pfx --certificate-password-env PDF_SIGNING_PASSWORD --out approved.pdf",
            ],
            [
                new("aspose-cli docs pdf/editing", "fixed-layout operations and safe mutation"),
                new("aspose-cli docs pdf/verification", "read-back, rendering and PDF/A evidence"),
                new("aspose-cli schema v2/pdf/ops", "the operation JSON schema"),
            ]);
    }
}
