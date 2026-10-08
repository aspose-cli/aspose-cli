using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class EditCommand
{
    private static readonly BoundedEditDefinition<SlidesOp, SlidesOpsBatch> Definition = new()
    {
        Contracts = ProductJsonContext.Definition,
        Writes = SlidesFormats.Writable,
    };

    public static Command Create(IProductCommandHost<ISlidesEngine> host) =>
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
            (parse, edit, standard) =>
            {
                Secret? encryptPassword = standard.EncryptPassword();
                return standard.OpenEngine().ApplyOps(standard.Input, edit.Batch, new PresentationEditRequest
                {
                    Output = standard.Output,
                    Options = edit.Options,
                    Password = standard.InputPassword,
                    EncryptPassword = encryptPassword,
                });
            });
}
