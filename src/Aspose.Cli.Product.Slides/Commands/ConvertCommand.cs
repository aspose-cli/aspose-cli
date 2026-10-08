using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.IO;

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
                ResolvedOutput output = standard.Output;
                string? range = parse.GetValue(slides);
                Secret? encryptPassword = standard.EncryptPassword();
                return standard.OpenEngine().Convert(standard.Input, new PresentationConvertRequest
                {
                    Output = output,
                    Slides = range is null ? null : PageRange.Parse(range),
                    Password = standard.InputPassword,
                    EncryptPassword = encryptPassword,
                });
            });
    }
}
