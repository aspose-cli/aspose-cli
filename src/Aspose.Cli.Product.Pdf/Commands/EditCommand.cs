using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class EditCommand
{
    public static CommandDefinition<PdfEditRequest, PdfEditResult> Create() =>
        TableCommands.Edit<PdfEditRequest, PdfEditResult>(
            PdfModule.Table,
            "Apply a batch of set, add, delete and do operations to a PDF; nothing is written when one fails.",
            new CommandTraits
            {
                Input = PdfInputs.Document,
                Encrypt = PdfInputs.EncryptedPdf,
                UsesFonts = true,
            },
            PdfFormats.Document,
            static edit => new PdfEditRequest
            {
                Input = edit.Input,
                Batch = edit.Batch,
                Output = edit.Output,
                Password = edit.Password,
                EncryptPassword = edit.EncryptPassword,
                EncryptOption = edit.EncryptOption,
            },
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
