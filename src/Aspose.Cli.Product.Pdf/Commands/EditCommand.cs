using System.CommandLine;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class EditCommand
{
    private static readonly BoundedEditDefinition<PdfOp, PdfOpsBatch> Definition = new()
    {
        Contracts = ProductJsonContext.Definition,
        VerifyDescription = "Read the staged output back and check the effect of each form, redaction, bookmark, metadata, attachment and page operation.",
    };

    public static Command Create(IProductCommandHost<IPdfEngine> host) =>
        new BoundedEditCommand<PdfOp, PdfOpsBatch>(Definition).Create(
            host,
            "edit",
            "Apply one validated, atomic PDF operation batch.",
            new CommandTraits
            {
                Input = PdfCommands.Document,
                UsesFonts = true,
            },
            [],
            (_, edit, standard) => standard.OpenEngine().ApplyOps(standard.Input, edit.Batch, new PdfEditRequest
            {
                OutputPath = edit.Target.OutputPath,
                Overwrite = edit.Target.Overwrite,
                BackupPath = edit.Target.BackupPath,
                Options = edit.Options,
                Password = standard.InputPassword,
                Verify = edit.Verify,
                OpSecrets = edit.Secrets,
            }));
}
