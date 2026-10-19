using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class EditCommand
{
    public static CommandDefinition<SlidesEditRequest, SlidesEditResult> Create() =>
        EditDefinition.Create<SlidesOp, SlidesOpsBatch, SlidesEditRequest, SlidesEditResult>(
            new BoundedEditCommand<SlidesOp, SlidesOpsBatch>(new BoundedEditDefinition<SlidesOp, SlidesOpsBatch>
            {
                Contracts = ProductJsonContext.Definition,
                VerifyDescription = "Read the staged output back and check the slide order, titles, shape text, body paragraphs, notes, layouts, hidden slides, footers, transitions, sections, properties, slide size and replaced text each operation left.",
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
            Table,
            examples:
            [
                "slides edit deck.pptx --ops deck-ops.json --out revised.pptx --verify",
                "slides edit deck.pptx --in-place --backup --ops deck-ops.json",
            ],
            links:
            [
                CommandHelpLink.Docs(SlidesModule.Manifest, "editing", "atomic presentation operations"),
                CommandHelpLink.Schema<SlidesOp>("the operation JSON schema"),
                CommandHelpLink.Docs(SlidesModule.Manifest, "verification", "what --verify reads back"),
            ],
            // Presentations merge by appending with the import_slides operation.
            mistakenFor: ["merge"],
            mistakenForHint: "To merge presentations, run 'slides edit' on the first with an import_slides operation for each other presentation.");

    internal static void Table(SlidesEditResult result, TableSurface surface)
    {
        ResultText.Edit(surface, result.DryRun, result.Output, result.Applied, result.Backup);
        ResultText.Touched(surface, "slides", result.SlidesTouched);
    }
}
