using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class ExtractCommand
{
    public static CommandDefinition<SlidesExtractRequest, SlidesExtractResult> Create()
    {
        var what = new Option<string>("--what") { Required = true, Description = "What to extract: media, the pictures, audio and video the presentation embeds." }.WithInput(InputKind.None);
        what.AcceptOnlyFromAmong([.. SlidesExtractKinds.All]);
        var slides = new PartRangeOption("slide", "every slide");
        return new(
            "extract",
            "Extract the media a presentation embeds, byte for byte.",
            new CommandTraits
            {
                Input = SlidesInputs.Presentation,
                Output = OutputTarget.Directory("Safe extraction directory."),
            },
            [what, .. slides.Options],
            (parse, standard) =>
            {
                return new SlidesExtractRequest
                {
                    Input = standard.Input,
                    What = parse.GetRequiredValue(what),
                    Output = standard.DirectoryOutput,
                    Slides = slides.ReadRange(parse),
                    Password = standard.InputPassword,
                };
            })
        {
            Examples =
            [
                "slides extract deck.pptx --what media --out-dir media",
                "slides extract deck.pptx --what media --slides 2-4 --out-dir media",
            ],
            Links =
            [
                CommandHelpLink.Docs(SlidesModule.Manifest, "overview", "what each Slides command is for"),
            ],
        };
    }
}
