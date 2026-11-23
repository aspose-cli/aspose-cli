using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class EditCommand
{
    public static CommandDefinition<TableEditRequest, PdfEditResult> Create() =>
        TableCommands.Edit<PdfEditResult>(
            PdfModule.Table,
            "Apply a batch of set, add, delete and do operations to a PDF; nothing is written when one fails.",
            new CommandTraits
            {
                Input = PdfInputs.Document,
                Encrypt = PdfInputs.EncryptedPdf,
                UsesFonts = true,
            },
            PdfFormats.Document,
            examples:
            [
                "pdf edit form.pdf --ops ops.json --out filled.pdf",
                "pdf edit report.pdf --in-place --ops ops.json",
            ],
            links:
            [
                CommandHelpLink.Schema(PdfModule.Manifest, "edit-result", "the fields of the edit result"),
            ]);
}
