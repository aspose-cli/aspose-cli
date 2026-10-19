using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Extensibility.Output;
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
                "slides convert deck.pptx --to html5 --out site/deck.html",
            ],
            Links =
            [
                CommandHelpLink.Docs(SlidesModule.Manifest, "overview", "what each Slides command is for"),
            ],
        };
    }

    internal static void Table(SlidesConvertResult result, TableSurface surface)
    {
        ResultText.Files(surface, "wrote", "file", [.. result.Outputs.Select(output =>
            new ProducedFile(
                null,
                output.Path,
                output.Format,
                output.SizeBytes,
                result.Slides is null ? null : $"slides {result.Slides}"))]);
    }
}
