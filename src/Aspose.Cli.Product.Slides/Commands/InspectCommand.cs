using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class InspectCommand
{
    public static CommandDefinition<SlidesInspectRequest, SlidesInspectResult> Create()
    {
        var sample = new SampleOption($"each slide's title and up to {SlidesSlideInfo.PreviewTextLength} characters of its text");
        var detail = new DetailOption(
            SlidesInspectDetails.All,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [SlidesInspectDetails.Notes] = "presence and character count, with the text from 'slides read --notes'",
            });
        return new(
            "inspect",
            "Show presentation structure, stable slide ids and metadata.",
            new CommandTraits { Input = SlidesInputs.Presentation },
            [.. sample.Options, .. detail.Options],
            (parse, standard) => new SlidesInspectRequest
            {
                Input = standard.Input,
                IncludePreview = sample.Read(parse),
                Details = detail.Read(parse),
                Password = standard.InputPassword,
            })
        {
            Examples =
            [
                "slides inspect deck.pptx --output-format json",
                "slides inspect deck.pptx --sample --detail layouts --detail notes",
            ],
            Links =
            [
                CommandHelpLink.Docs(SlidesModule.Manifest, "overview", "what each Slides command is for"),
                new("aspose-cli docs reading", "reading every product within bounded windows"),
            ],
        };
    }
}
