using System.CommandLine;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class RenderCommand
{
    public static Command Create(IProductCommandHost<IPresentationEngine> host)
    {
        Argument<string> file = SlidesOptions.File();
        var to = new Option<string>("--to") { DefaultValueFactory = _ => "png", Description = "png, jpeg or svg." };
        to.AcceptOnlyFromAmong([.. SlidesModule.Formats.IdsFor(FormatUse.Render)]);
        var slide = new Option<int?>("--slide") { Description = "One 1-based slide number." };
        var slides = new Option<string?>("--slides") { Description = "A 1-based slide range." };
        var allSlides = new Option<bool>("--all-slides") { Description = "Render every slide." };
        var dpi = new Option<int?>("--dpi") { Description = "Raster resolution; defaults to 192." };
        var width = new Option<int?>("--width") { Description = "Exact raster width in pixels." };
        var output = new OutputFileOptions(
            "Output path; multi-slide output adds .sN before the extension.");
        var password = new PasswordOptions("--password", "the presentation");
        var command = new Command("render", "Render one or more slides for visual verification.");
        command.Arguments.Add(file);
        command.Options.Add(to);
        command.Options.Add(slide);
        command.Options.Add(slides);
        command.Options.Add(allSlides);
        command.Options.Add(dpi);
        command.Options.Add(width);
        output.AddTo(command);
        password.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            int? one = parse.GetValue(slide);
            string? range = parse.GetValue(slides);
            bool every = parse.GetValue(allSlides);
            if ((one is null ? 0 : 1) + (range is null ? 0 : 1) + (every ? 1 : 0) > 1)
            {
                throw CliErrors.Usage(["Choose only one of --slide, --slides or --all-slides."]);
            }

            if (one is <= 0)
            {
                throw CliErrors.OptionInvalid("--slide", "must be positive", "Slides are 1-based.");
            }

            int? resolution = parse.GetValue(dpi);
            int? pixelWidth = parse.GetValue(width);
            if (resolution is not null && pixelWidth is not null)
            {
                throw CliErrors.Usage(["Choose --dpi or --width, not both."]);
            }

            if (resolution is not null)
            {
                OptionGuards.EnsureInRange("--dpi", resolution.Value, 36, 1200, "Use 36-1200 DPI.");
            }

            if (pixelWidth is not null)
            {
                OptionGuards.EnsureInRange("--width", pixelWidth.Value, 64, 20_000, "Use 64-20000 pixels.");
            }

            string format = parse.GetValue(to) ?? "png";
            if (format == "svg" && (resolution is not null || pixelWidth is not null))
            {
                throw CliErrors.Usage(["SVG is vector output; omit --dpi and --width."]);
            }

            string input = context.Paths.ResolveInput(parse.GetRequiredValue(file));
            string? selection = one is null ? range : one.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return context.Port.Render(input, new PresentationRenderRequest
            {
                TargetFormatId = format,
                OutputPath = output.ResolvePath(parse, context.Paths, input, SlidesModule.Formats.ExtensionFor(format)),
                Overwrite = output.Overwrite(parse),
                Slides = selection is null ? null : PageRange.Parse(selection),
                AllSlides = every,
                Dpi = resolution,
                Width = pixelWidth,
                Password = password.Resolve(parse, context.Inputs),
            });
        }));
        return command;
    }
}
