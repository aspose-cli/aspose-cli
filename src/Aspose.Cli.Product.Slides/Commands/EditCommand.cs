using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class EditCommand
{
    public static CommandDefinition<PresentationEditRequest, SlidesEditResult> Create() =>
        EditDefinition.Create<SlidesOp, SlidesOpsBatch, PresentationEditRequest, SlidesEditResult>(
            new BoundedEditCommand<SlidesOp, SlidesOpsBatch>(new BoundedEditDefinition<SlidesOp, SlidesOpsBatch>
            {
                Contracts = ProductJsonContext.Definition,
                Writes = SlidesFormats.Writable,
            }),
            "Apply one validated, atomic presentation operation batch.",
            new CommandTraits
            {
                Input = SlidesInputs.Presentation,
                Encrypt = SlidesInputs.EncryptedPresentation,
                UsesFonts = true,
            },
            [],
            (_, edit, standard) =>
            {
                Secret? encryptPassword = standard.EncryptPassword();
                return new PresentationEditRequest
                {
                    Input = standard.Input,
                    Batch = edit.Batch,
                    Output = standard.Output,
                    Options = edit.Options,
                    Password = standard.InputPassword,
                    EncryptPassword = encryptPassword,
                };
            },
            Table);

    internal static void Table(SlidesEditResult result, TableSurface surface)
    {
        ResultText.Edit(surface, result.DryRun, result.Output, result.Applied, result.Backup);
        if (result.SlidesTouched is { Count: > 0 } slides)
        {
            surface.Out.WriteLine($"slide ids touched: {string.Join(", ", slides)}");
        }
    }
}
