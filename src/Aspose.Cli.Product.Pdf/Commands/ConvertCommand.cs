using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class ConvertCommand
{
    public static Command Create(IProductCommandHost<IPdfEngine> host)
    {
        var pages = new Option<string?>("--pages") { Description = "Optional 1-based page range." }.WithInput(InputKind.None);
        return StandardCommand.Create(
            host,
            "convert",
            "Convert selected PDF pages to a supported format.",
            new CommandTraits
            {
                Input = PdfCommands.Document,
                Output = OutputTarget.File("Output path; defaults to a sibling using the target extension."),
                UsesFonts = true,
                Target = TargetFormat.Convert("Target PDF export format.", PdfFormats.Definitions),
            },
            [pages],
            (parse, standard) =>
            {
                ResolvedOutput output = standard.Output;
                string? range = parse.GetValue(pages);
                return standard.OpenEngine().Convert(standard.Input, new PdfConvertRequest
                {
                    Output = output,
                    Pages = range is null ? null : PageRange.Parse(range),
                    Password = standard.InputPassword,
                });
            });
    }
}
