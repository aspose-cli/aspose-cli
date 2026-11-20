using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class ReadCommand
{
    public static CommandDefinition<SlidesReadRequest, SlidesReadResult> Create() =>
        TableCommands.Read<SlidesReadRequest, SlidesReadResult>(
            SlidesModule.Table,
            "Read a presentation's objects, each with every row of the object table, and its inventory.",
            new CommandTraits { Input = SlidesInputs.Presentation },
            static read => new SlidesReadRequest
            {
                Input = read.Input,
                Password = read.Password,
                At = read.At,
                From = read.From,
                Kinds = read.Kinds,
                MaxObjects = read.MaxObjects,
            },
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
