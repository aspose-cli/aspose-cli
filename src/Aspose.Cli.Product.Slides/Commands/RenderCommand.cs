using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class RenderCommand
{
    public static CommandDefinition<SlidesRenderRequest, SlidesRenderResult> Create()
    {
        var slides = new PartSelectionOptions("slide");
        var width = new Option<int?>("--width")
        {
            Description = "Exact raster width in pixels; replaces --dpi and is refused for vector output such as svg.",
        }.InRange(64, 20_000);
        var dpi = new DpiOption(width);
        return new(
            "render",
            "Render one or more slides for visual verification.",
            new CommandTraits
            {
                Input = SlidesInputs.Presentation,
                Output = OutputTarget.File("Output path. Default: the input path with the image extension. "
                    + "Several slides write <stem>.<slide>.<ext> beside it."),
                UsesFonts = true,
                Target = TargetFormat.Render(SlidesFormats.Definitions),
            },
            [.. slides.Options, .. dpi.Options, width],
            (parse, standard) =>
            {
                PageRange? selection = slides.Read(parse);
                ResolvedOutput output = standard.Output;
                int resolution = dpi.Read(parse, output.Format);
                bool explicitDpi = dpi.IsExplicit(parse);
                int? pixelWidth = parse.GetInRange(width);
                if (explicitDpi && pixelWidth is not null)
                {
                    throw CliErrors.OptionInvalid(
                        "--width",
                        "cannot be combined with --dpi");
                }

                return new SlidesRenderRequest
                {
                    Input = standard.Input,
                    Output = output,
                    Slides = selection,
                    Dpi = pixelWidth is null ? resolution : null,
                    Width = pixelWidth,
                    Password = standard.InputPassword,
                };
            })
        {
            Examples =
            [
                "slides render deck.pptx --slides 1-3 --out slide.png",
                "slides render deck.pptx --slides 2 --width 1920 --out hero.png",
            ],
            Links =
            [
                CommandHelpLink.Docs(SlidesModule.Manifest, "engine-behavior", "what the engine draws and saves as it is"),
            ],
        };
    }
}
