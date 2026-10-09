using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Extensibility.Output;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class ConvertCommand
{
    public static CommandDefinition<PresentationConvertRequest, SlidesConvertResult> Create()
    {
        var slides = new Option<string?>("--slides") { Description = "Optional 1-based slide range." }.WithInput(InputKind.None);
        return new(
            "convert",
            "Convert a presentation or selected slides.",
            new CommandTraits
            {
                Input = SlidesInputs.Presentation,
                Output = OutputTarget.File("Output path; defaults to a sibling using the target extension; multi-slide image output adds .sN before the extension."),
                Encrypt = SlidesInputs.EncryptedPresentation,
                UsesFonts = true,
                Target = TargetFormat.Convert("Target presentation export format. PNG and JPEG use 192 DPI; use slides render for custom dimensions.", SlidesFormats.Definitions),
            },
            [slides],
            (parse, standard) =>
            {
                ResolvedOutput output = standard.Output;
                string? range = parse.GetValue(slides);
                Secret? encryptPassword = standard.EncryptPassword();
                return new PresentationConvertRequest
                {
                    Input = standard.Input,
                    Output = output,
                    Slides = range is null ? null : PageRange.Parse(range),
                    Password = standard.InputPassword,
                    EncryptPassword = encryptPassword,
                };
            },
            Table);
    }

    internal static void Table(SlidesConvertResult result, TableSurface surface)
    {
        foreach (var output in result.Outputs)
        {
            ResultText.Produced(surface, output, result.Slides is null ? null : $"slides {result.Slides}");
        }
    }
}
