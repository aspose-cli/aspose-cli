using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class ConvertCommand
{
    public static CommandDefinition<SlidesConvertRequest, SlidesConvertResult> Create()
    {
        var slides = new PartRangeOption("slide", "every slide");
        return new(
            "convert",
            "Convert a presentation or selected slides.",
            new CommandTraits
            {
                Input = SlidesInputs.Presentation,
                Output = OutputTarget.File("Output path; defaults to a sibling using the target extension."),
                Encrypt = SlidesInputs.EncryptedPresentation,
                UsesFonts = true,
                Target = TargetFormat.Convert("Target presentation export format; 'slides render' writes slide images.", SlidesFormats.Definitions),
            },
            [.. slides.Options],
            (parse, standard) =>
            {
                ResolvedOutput output = standard.Output;
                PageRange? range = slides.ReadRange(parse);
                Secret? encryptPassword = standard.EncryptPassword();
                return new SlidesConvertRequest
                {
                    Input = standard.Input,
                    Output = output,
                    Slides = range,
                    Password = standard.InputPassword,
                    EncryptPassword = encryptPassword,
                };
            })
        {
            Examples =
            [
                "slides convert deck.pptx --out deck.pdf",
                "slides convert deck.pptx --slides 1-3 --out handout.pdf",
                "slides convert deck.pptx --to html5 --out site/deck.html",
            ],
            Links =
            [
                CommandHelpLink.Schema(SlidesModule.Manifest, "convert-result", "the fields of the convert result"),
            ],
        };
    }
}
