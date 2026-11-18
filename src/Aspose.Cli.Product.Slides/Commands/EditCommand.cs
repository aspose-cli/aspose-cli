using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class EditCommand
{
    public static CommandDefinition<SlidesEditRequest, SlidesEditResult> Create() =>
        EditDefinition.Create<SlidesOp, SlidesOpsBatch, SlidesEditRequest, SlidesEditResult>(
            new BoundedEditCommand<SlidesOp, SlidesOpsBatch>(new BoundedEditDefinition<SlidesOp, SlidesOpsBatch>
            {
                Contracts = ProductJsonContext.Definition,
                VerifyDescription = "Read the staged output back and check the slide order, titles, shape text, body paragraphs, notes, layouts, hidden slides, footers, transitions, sections, properties, slide size and replaced text each operation left; inserted pictures, shapes, tables and charts are not read back.",
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
            static (_, edit, _) => new SlidesEditRequest(edit),
            examples:
            [
                "slides edit deck.pptx --ops deck-ops.json --out revised.pptx --verify",
                "slides edit deck.pptx --in-place --ops deck-ops.json",
            ],
            links:
            [
                CommandHelpLink.Docs(SlidesModule.Manifest, "editing", "atomic presentation operations"),
                CommandHelpLink.Schema<SlidesOp>("the operation JSON schema"),
                CommandHelpLink.Docs(SlidesModule.Manifest, "verification", "what --verify reads back"),
            ]);
}
