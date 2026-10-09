using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility.Output;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class ConvertCommand
{
    public static CommandDefinition<PdfConvertRequest, PdfConvertResult> Create()
    {
        var pages = new Option<string?>("--pages") { Description = "Optional 1-based page range." }.WithInput(InputKind.None);
        return new(
            "convert",
            "Convert selected PDF pages to a supported format.",
            new CommandTraits
            {
                Input = PdfInputs.Document,
                Output = OutputTarget.File("Output path; defaults to a sibling using the target extension."),
                UsesFonts = true,
                Target = TargetFormat.Convert("Target PDF export format.", PdfFormats.Definitions),
            },
            [pages],
            (parse, standard) =>
            {
                ResolvedOutput output = standard.Output;
                string? range = parse.GetValue(pages);
                return new PdfConvertRequest
                {
                    Input = standard.Input,
                    Output = output,
                    Pages = range is null ? null : PageRange.Parse(range),
                    Password = standard.InputPassword,
                };
            },
            Table);
    }

    internal static void Table(PdfConvertResult result, TableSurface surface)
    {
        foreach (var output in result.Outputs)
        {
            ResultText.Produced(surface, output, result.Pages is null ? null : $"pages {result.Pages}");
        }
    }
}
