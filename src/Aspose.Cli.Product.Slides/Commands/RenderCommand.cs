using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class RenderCommand
{
    public static Command Create(IProductCommandHost<ISlidesEngine> host)
    {
        var slides = new PartSelectionOptions("slide");
        var dpi = new DpiOption();
        var width = new Option<int?>("--width") { Description = "Exact raster width in pixels (64-20000); replaces --dpi." };
        return StandardCommand.Create(
            host,
            "render",
            "Render one or more slides for visual verification.",
            new CommandTraits
            {
                Input = SlidesCommands.Presentation,
                Output = OutputTarget.File("Output path; multi-slide output adds .sN before the extension."),
                UsesFonts = true,
                Target = TargetFormat.Render("png, jpeg or svg.", SlidesFormats.Definitions),
            },
            [.. slides.Options, .. dpi.Options, width],
            (parse, standard) =>
            {
                PartSelection selection = slides.Read(parse);
                int resolution = dpi.Read(parse);
                bool explicitDpi = dpi.IsExplicit(parse);
                int? pixelWidth = parse.GetValue(width);
                if (explicitDpi && pixelWidth is not null)
                {
                    throw CliErrors.Usage(["Choose --dpi or --width, not both."]);
                }

                if (pixelWidth is not null)
                {
                    OptionGuards.EnsureInRange("--width", pixelWidth.Value, 64, 20_000, "Use 64-20000 pixels.");
                }

                ResolvedOutput output = standard.Output;
                if (output.Format.Id == "svg" && (explicitDpi || pixelWidth is not null))
                {
                    throw CliErrors.Usage(["SVG is vector output; omit --dpi and --width."]);
                }

                return standard.OpenEngine().Render(standard.Input, new PresentationRenderRequest
                {
                    Output = output,
                    Slides = selection.Range,
                    AllSlides = selection.All,
                    Dpi = pixelWidth is null ? resolution : null,
                    Width = pixelWidth,
                    Password = standard.InputPassword,
                });
            });
    }
}
