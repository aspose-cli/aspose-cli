using System.CommandLine;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class ConvertCommand
{
    public static Command Create(IProductCommandHost<IPdfEngine> host)
    {
        var to = new Option<string>("--to") { Required = true, Description = "Target PDF export format." }.WithInput(InputKind.None);
        to.AcceptOnlyFromAmong([.. PdfFormats.Definitions.IdsFor(FormatUse.Convert)]);
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
            },
            [to, pages],
            (parse, standard) =>
            {
                string format = parse.GetRequiredValue(to);
                string? range = parse.GetValue(pages);
                return standard.Port.Convert(standard.Input, new PdfConvertRequest
                {
                    TargetFormatId = format,
                    OutputPath = standard.OutputPath(PdfFormats.Definitions.ExtensionFor(format)),
                    Overwrite = standard.Overwrite,
                    Pages = range is null ? null : PageRange.Parse(range),
                    Password = standard.InputPassword,
                });
            });
    }
}
