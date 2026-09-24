using System.CommandLine;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class ConvertCommand
{
    public static Command Create(IProductCommandHost<IPresentationEngine> host)
    {
        var to = new Option<string>("--to") { Required = true, Description = "Target presentation export format. PNG and JPEG use 192 DPI; use slides render for custom dimensions." }.WithInput(InputKind.None);
        to.AcceptOnlyFromAmong([.. SlidesFormats.Definitions.IdsFor(FormatUse.Convert)]);
        var slides = new Option<string?>("--slides") { Description = "Optional 1-based slide range." }.WithInput(InputKind.None);
        return StandardCommand.Create(
            host,
            "convert",
            "Convert a presentation or selected slides.",
            new CommandTraits
            {
                Input = SlidesCommands.Presentation,
                Output = OutputTarget.File("Output path; defaults to a sibling using the target extension."),
                Encrypt = SlidesCommands.EncryptedPresentation,
                UsesFonts = true,
            },
            [to, slides],
            (parse, standard) =>
            {
                string format = parse.GetRequiredValue(to);
                string? range = parse.GetValue(slides);
                return standard.Port.Convert(standard.Input, new PresentationConvertRequest
                {
                    TargetFormatId = format,
                    OutputPath = standard.OutputPath(SlidesFormats.Definitions.ExtensionFor(format)),
                    Overwrite = standard.Overwrite,
                    Slides = range is null ? null : PageRange.Parse(range),
                    Password = standard.InputPassword,
                    EncryptPassword = standard.EncryptPassword(format),
                });
            });
    }
}
