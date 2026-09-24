using System.CommandLine;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

/// <summary>The PDF product command group.</summary>
internal static class PdfCommands
{
    /// <summary>The PDF every reading command opens.</summary>
    public static readonly InputDocument Document = new("PDF document to open.", "the PDF");

    public static Command Create(IProductCommandHost<IPdfEngine> host)
    {
        var pdf = new Command("pdf", "PDF automation with page, security and fixed-layout semantics.");
        pdf.Subcommands.Add(InfoCommand.Create(host));
        pdf.Subcommands.Add(QueryCommand.Create(host));
        pdf.Subcommands.Add(ConvertCommand.Create(host));
        pdf.Subcommands.Add(RenderCommand.Create(host));
        pdf.Subcommands.Add(NewCommand.Create(host));
        pdf.Subcommands.Add(MergeCommand.Create(host));
        pdf.Subcommands.Add(SplitCommand.Create(host));
        pdf.Subcommands.Add(ExtractCommand.Create(host));
        pdf.Subcommands.Add(EditCommand.Create(host));
        pdf.Subcommands.Add(ValidateCommand.Create(host));
        pdf.Subcommands.Add(SignCommand.Create(host));
        return pdf.WithExamples(
            [
                "pdf inspect report.pdf --preview --detail permissions forms signatures",
                "pdf query pages report.pdf --pages 1-5 --mode layout --output json",
                "pdf edit report.pdf --ops ops.json --out reviewed.pdf",
                "pdf sign reviewed.pdf --certificate signer.pfx --certificate-password-env PDF_SIGNING_PASSWORD --out approved.pdf",
            ],
            [
                CommandHelpLink.Docs($"{PdfModule.Manifest.Id}/editing", "fixed-layout operations and safe mutation"),
                CommandHelpLink.Docs($"{PdfModule.Manifest.Id}/verification", "read-back, rendering and PDF/A evidence"),
                CommandHelpLink.Schema(PdfModule.Manifest.Operations.Single(), "the operation JSON schema"),
            ]);
    }
}
