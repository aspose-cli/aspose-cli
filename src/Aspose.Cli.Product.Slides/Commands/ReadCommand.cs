using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class ReadCommand
{
    public static CommandDefinition<TableReadRequest, SlidesReadResult> Create() =>
        TableCommands.Read<SlidesReadResult>(
            SlidesModule.Table,
            "Read a presentation's objects, each with every row of the object table, and its inventory.",
            new CommandTraits { Input = SlidesInputs.Presentation },
            examples:
            [
                "slides read deck.pptx",
                "slides read deck.pptx --at slide/256 --kind shape",
            ],
            links:
            [
                CommandHelpLink.Schema(SlidesModule.Manifest, "read-result", "the fields of the read result"),
            ]);
}
