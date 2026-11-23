using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class EditCommand
{
    public static CommandDefinition<TableEditRequest, SlidesEditResult> Create() =>
        TableCommands.Edit<SlidesEditResult>(
            SlidesModule.Table,
            "Apply a batch of set, add, delete, move and do operations to a presentation; nothing is written when one fails.",
            new CommandTraits
            {
                Input = SlidesInputs.Presentation,
                Encrypt = SlidesInputs.EncryptedPresentation,
                UsesFonts = true,
            },
            SlidesFormats.Editable,
            examples:
            [
                "slides edit deck.pptx --ops ops.json --out revised.pptx",
                "slides edit deck.pptx --in-place --ops ops.json",
            ],
            links:
            [
                CommandHelpLink.Schema(SlidesModule.Manifest, "edit-result", "the fields of the edit result"),
            ]);
}
