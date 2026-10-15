using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Extensibility.Output;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class RenderCommand
{
    public static CommandDefinition<SlidesRenderRequest, SlidesRenderResult> Create()
    {
        var slides = new PartSelectionOptions("slide");
        var dpi = new DpiOption();
        var width = new Option<int?>("--width") { Description = "Exact raster width in pixels; replaces --dpi." }.InRange(64, 20_000);
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
                int resolution = dpi.Read(parse);
                bool explicitDpi = dpi.IsExplicit(parse);
                int? pixelWidth = parse.GetInRange(width);
                if (explicitDpi && pixelWidth is not null)
                {
                    throw CliErrors.OptionInvalid(
                        "--width",
                        "cannot be combined with --dpi",
                        "Drop --dpi to render at an exact pixel width, or drop --width to render at a DPI.");
                }

                ResolvedOutput output = standard.Output;
                if (output.Format.Id == "svg" && (explicitDpi || pixelWidth is not null))
                {
                    throw CliErrors.Usage(["SVG is vector output; omit --dpi and --width."]);
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
            },
            Table)
        {
            Examples =
            [
                "slides render deck.pptx --slides 1-3 --out slide.png",
                "slides render deck.pptx --slides 2 --width 1920 --out hero.png",
            ],
            Links =
            [
                CommandHelpLink.Docs(SlidesModule.Manifest, "verification", "slide read-back and visual review"),
            ],
        };
    }

    internal static void Table(SlidesRenderResult result, TableSurface surface)
    {
        ResultText.Files(surface, "rendered", "slide", [.. result.Outputs.Select(static item =>
            new ProducedFile($"slide {item.Slide}", item.Path, item.Format, item.SizeBytes))]);
    }
}
