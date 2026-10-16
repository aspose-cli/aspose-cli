using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class ExtractCommand
{
    public static CommandDefinition<SlidesExtractRequest, SlidesExtractResult> Create()
    {
        var what = new Option<string>("--what") { Required = true, Description = "What to extract; media includes audio and video." }.WithInput(InputKind.None);
        what.AcceptOnlyFromAmong([.. SlidesExtractKinds.All]);
        var slides = new PartRangeOption("slide", "every slide");
        return new(
            "extract",
            "Extract bounded presentation media, notes or text.",
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
            },
            Table)
        {
            Examples =
            [
                "slides extract deck.pptx --what notes --out-dir notes",
                "slides extract deck.pptx --what media --slides 2-4 --out-dir media",
            ],
            Links =
            [
                CommandHelpLink.Docs(SlidesModule.Manifest, "overview", "what each Slides command is for"),
            ],
        };
    }

    internal static void Table(SlidesExtractResult result, TableSurface surface)
    {
        ResultText.Files(surface, "extracted", $"{result.What} item", [.. result.Outputs.Select(static item =>
            new ProducedFile(item.Slide is { } slide ? $"slide {slide}" : $"media {item.Index}", item.Path, item.Format, item.SizeBytes))]);
    }
}
