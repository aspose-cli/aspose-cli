using System.CommandLine;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Slides.Commands;

/// <summary>The Slides product command group.</summary>
internal static class SlidesCommands
{
    /// <summary>The presentation every reading command opens.</summary>
    public static readonly InputDocument Presentation = new("Presentation file path.", "the presentation");

    /// <summary>The password a writing command can put on its presentation.</summary>
    public static readonly EncryptedOutput EncryptedPresentation = new("the output presentation", SlidesFormats.EncryptIds);

    public static Command Create(IProductCommandHost<IPresentationEngine> host)
    {
        var slides = new Command("slides", "Presentation automation with slide, layout and notes semantics.");
        slides.Subcommands.Add(InfoCommand.Create(host));
        slides.Subcommands.Add(QueryCommand.Create(host));
        slides.Subcommands.Add(ConvertCommand.Create(host));
        slides.Subcommands.Add(RenderCommand.Create(host));
        slides.Subcommands.Add(NewCommand.Create(host));
        slides.Subcommands.Add(EditCommand.Create(host));
        slides.Subcommands.Add(ExtractCommand.Create(host));
        return slides.WithExamples(
            [
                "slides create deck.pptx --from-markdown outline.md --template brand.pptx",
                "slides inspect deck.pptx --preview --detail masters layouts fonts notes",
                "slides query slides deck.pptx --slides 1-5 --scope full --notes --output json",
                "slides edit deck.pptx --ops deck-ops.json --out revised.pptx",
                "review revised.pptx --out revised.review",
            ],
            [
                CommandHelpLink.Docs(SlidesModule.Manifest, "editing", "atomic presentation operations"),
                CommandHelpLink.Docs(SlidesModule.Manifest, "verification", "slide read-back and visual review"),
                CommandHelpLink.Schema(SlidesModule.Manifest, "the operation JSON schema"),
            ]);
    }
}
