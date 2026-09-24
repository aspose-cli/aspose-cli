using System.CommandLine;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class RenderCommand
{
    public static Command Create(IProductCommandHost<IPresentationEngine> host)
    {
        Argument<string> file = SlidesOptions.File();
        var to = new Option<string>("--to") { DefaultValueFactory = _ => "png", Description = "png, jpeg or svg." }.WithInput(InputKind.None);
        to.AcceptOnlyFromAmong([.. SlidesFormats.Definitions.IdsFor(FormatUse.Render)]);
        var slides = new PartSelectionOptions("slide");
        var dpi = new DpiOption();
        var width = new Option<int?>("--width") { Description = "Exact raster width in pixels (64-20000); replaces --dpi." };
        var output = new OutputFileOptions(
            "Output path; multi-slide output adds .sN before the extension.");
        var password = new PasswordOptions("--password", "the presentation");
        var fonts = new FontDirectoryOptions();
        var command = new Command("render", "Render one or more slides for visual verification.");
        command.Arguments.Add(file);
        command.Options.Add(to);
        slides.AddTo(command);
        dpi.AddTo(command);
        command.Options.Add(width);
        output.AddTo(command);
        password.AddTo(command);
        fonts.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
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

            string format = parse.GetValue(to) ?? "png";
            if (format == "svg" && (explicitDpi || pixelWidth is not null))
            {
                throw CliErrors.Usage(["SVG is vector output; omit --dpi and --width."]);
            }

            string input = context.Paths.ResolveInput(parse.GetRequiredValue(file));
            using IDisposable fontScope = fonts.Use(parse, context);
            return context.Port.Render(input, new PresentationRenderRequest
            {
                TargetFormatId = format,
                OutputPath = output.ResolvePath(parse, context.Paths, input, SlidesFormats.Definitions.ExtensionFor(format)),
                Overwrite = output.Overwrite(parse),
                Slides = selection.Range,
                AllSlides = selection.All,
                Dpi = pixelWidth is null ? resolution : null,
                Width = pixelWidth,
                Password = password.Resolve(parse, context.Inputs, context.ReadEnvironment),
            });
        }));
        return command;
    }
}
