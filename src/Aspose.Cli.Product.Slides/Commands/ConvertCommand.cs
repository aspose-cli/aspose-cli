using System.CommandLine;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class ConvertCommand
{
    public static Command Create(IProductCommandHost<ISlidesEngine> host)
    {
        var slides = new Option<string?>("--slides") { Description = "Optional 1-based slide range." }.WithInput(InputKind.None);
        return StandardCommand.Create(
            host,
            "convert",
            "Convert a presentation or selected slides.",
            new CommandTraits
            {
                Input = SlidesCommands.Presentation,
                Output = OutputTarget.File("Output path; defaults to a sibling using the target extension; multi-slide image output adds .sN before the extension."),
                Encrypt = SlidesCommands.EncryptedPresentation,
                UsesFonts = true,
                Target = TargetFormat.Convert("Target presentation export format. PNG and JPEG use 192 DPI; use slides render for custom dimensions.", SlidesFormats.Definitions),
            },
            [slides],
            (parse, standard) =>
            {
                string format = standard.TargetFormat();
                string? range = parse.GetValue(slides);
                string? encryptPassword = standard.EncryptPassword(format);
                return standard.OpenEngine().Convert(standard.Input, new PresentationConvertRequest
                {
                    TargetFormatId = format,
                    OutputPath = standard.OutputPath(SlidesFormats.Definitions.ExtensionFor(format)),
                    Overwrite = standard.Overwrite,
                    Slides = range is null ? null : PageRange.Parse(range),
                    Password = standard.InputPassword,
                    EncryptPassword = encryptPassword,
                });
            });
    }
}
