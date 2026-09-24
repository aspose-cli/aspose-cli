using System.CommandLine;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class EditCommand
{
    private static readonly BoundedEditDefinition<PdfOp, PdfOpsBatch> Definition = new()
    {
        Catalog = PdfOps.Catalog,
        Contracts = ProductJsonContext.Definition,
        NormalizePaths = static (op, paths) => op switch
        {
            InsertPagesFromOp value => value with { Path = paths.ResolveInput(value.Path) },
            AddWatermarkImageOp value => value with { Path = paths.ResolveInput(value.Path) },
            AddStampImageOp value => value with { Path = paths.ResolveInput(value.Path) },
            AddAttachmentOp value => value with { Path = paths.ResolveInput(value.Path) },
            _ => op,
        },
        SecretVariables = static op => op switch
        {
            InsertPagesFromOp value => [value.PasswordEnv],
            EncryptPdfOp value => [value.OwnerPasswordEnv, value.UserPasswordEnv],
            _ => [],
        },
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
                OpSecrets = edit.Secrets,
            }));
}
