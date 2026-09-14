using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class SlidesHelpMetadata
{
    public static void Attach(Command root)
    {
        root.WithExamples(
            [
                "aspose-cli slides inspect deck.pptx --preview --detail masters layouts fonts notes",
                "aspose-cli slides query slides deck.pptx --slides 1-5 --scope full --notes --output json",
                "aspose-cli slides edit deck.pptx --ops deck-ops.json --out revised.pptx --verify",
                "aspose-cli slides render revised.pptx --all-slides --to png --width 1600 --out review.png",
            ],
            [
                new("aspose-cli docs slides/editing", "atomic presentation operations"),
                new("aspose-cli docs slides/verification", "slide read-back and visual evidence"),
                new("aspose-cli schema v2/slides/ops", "the operation JSON schema"),
            ]);
    }

    private static Command Find(Command root, string name) =>
        root.Subcommands.Single(command =>
            string.Equals(command.Name, name, StringComparison.Ordinal));
}
