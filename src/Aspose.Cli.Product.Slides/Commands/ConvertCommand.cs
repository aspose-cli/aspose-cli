using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Extensibility.Output;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Rendering;

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
                Output = OutputTarget.File("Output path; defaults to a sibling using the target extension; several slides as images write <stem>.<slide>.<ext> beside it."),
                Encrypt = SlidesInputs.EncryptedPresentation,
                UsesFonts = true,
                Target = TargetFormat.Convert($"Target presentation export format. PNG and JPEG use {RenderPixelGuard.DefaultDpi} DPI; use slides render for custom dimensions.", SlidesFormats.Definitions),
            },
            [.. slides.Options],
            (parse, standard) =>
            {
                ResolvedOutput output = standard.Output;
                Secret? encryptPassword = standard.EncryptPassword();
                return new SlidesConvertRequest
                {
                    Input = standard.Input,
                    Output = output,
                    Slides = slides.ReadRange(parse),
                    Password = standard.InputPassword,
                    EncryptPassword = encryptPassword,
                };
            },
            Table)
        {
            Examples =
            [
                "slides convert deck.pptx --out deck.pdf",
                "slides convert deck.pptx --slides 1-3 --out handout.pdf",
                "slides convert deck.pptx --to png --slides 2 --out slide.png",
            ],
            Links =
            [
                CommandHelpLink.Docs(SlidesModule.Manifest, "overview", "what each Slides command is for"),
            ],
        };
    }

    internal static void Table(SlidesConvertResult result, TableSurface surface)
    {
        foreach (var output in result.Outputs)
        {
            ResultText.Produced(surface, output, result.Slides is null ? null : $"slides {result.Slides}");
        }
    }
}
