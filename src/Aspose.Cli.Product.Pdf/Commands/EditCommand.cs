using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class EditCommand
{
    public static CommandDefinition<PdfEditRequest, PdfEditResult> Create() =>
        EditDefinition.Create<PdfOp, PdfOpsBatch, PdfEditRequest, PdfEditResult>(
            new BoundedEditCommand<PdfOp, PdfOpsBatch>(new BoundedEditDefinition<PdfOp, PdfOpsBatch>
            {
                Contracts = ProductJsonContext.Definition,
                VerifyDescription = "Read the staged output back and check the effect of each form, redaction, bookmark, metadata, attachment and page operation.",
                Writes = PdfFormats.Document,
            }),
            "Apply one validated, atomic PDF operation batch.",
            new CommandTraits
            {
                Input = PdfInputs.Document,
                Encrypt = PdfInputs.EncryptedPdf,
                UsesFonts = true,
            },
            [],
            static (_, edit, _) => new PdfEditRequest(edit),
            Table,
            examples:
            [
                "pdf edit report.pdf --ops ops.json --out reviewed.pdf --verify",
                "pdf edit report.pdf --in-place --backup --ops ops.json --best-effort",
            ],
            links:
            [
                CommandHelpLink.Docs(PdfModule.Manifest, "editing", "fixed-layout operations and safe mutation"),
                CommandHelpLink.Schema<PdfOp>("the operation JSON schema"),
                CommandHelpLink.Docs(PdfModule.Manifest, "verification", "what --verify reads back"),
            ]);

    internal static void Table(PdfEditResult result, TableSurface surface)
    {
        ResultText.Edit(surface, result.DryRun, result.Output, result.Applied, result.Backup);
        ResultText.Touched(surface, "pages", result.PagesTouched);
        ResultText.Verification(surface, result.Verification);
    }
}
