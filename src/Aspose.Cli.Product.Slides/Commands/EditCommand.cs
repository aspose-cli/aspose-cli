using System.CommandLine;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class EditCommand
{
    private static readonly BoundedEditDefinition<SlidesOp, SlidesOpsBatch> Definition = new()
    {
        Catalog = SlidesOps.Catalog,
        Contracts = ProductJsonContext.Definition,
        NormalizePaths = static (op, paths) => op switch
        {
            SetBackgroundOp { ImagePath: not null } value =>
                value with { ImagePath = paths.ResolveInput(value.ImagePath) },
            AppendPresentationOp value =>
                value with { Path = paths.ResolveInput(value.Path) },
            SlidesInsertImageOp value =>
                value with { Path = paths.ResolveInput(value.Path) },
            _ => op,
        },
    };

    public static Command Create(IProductCommandHost<IPresentationEngine> host) =>
        new BoundedEditCommand<SlidesOp, SlidesOpsBatch>(Definition).Create(
            host,
            "edit",
            "Apply one validated, atomic presentation operation batch.",
            new CommandTraits
            {
                Input = SlidesCommands.Presentation,
                Encrypt = SlidesCommands.EncryptedPresentation,
                UsesFonts = true,
            },
            [],
            (parse, edit, standard) => standard.Port.ApplyOps(standard.Input, edit.Batch, new PresentationEditRequest
            {
                OutputPath = edit.Target.OutputPath,
                Overwrite = edit.Target.Overwrite,
                BackupPath = edit.Target.BackupPath,
                Options = edit.Options,
                Password = standard.InputPassword,
                EncryptPassword = standard.EncryptPassword(SlidesFormats.ForOutput(edit.Target.OutputPath)),
            }));
}
