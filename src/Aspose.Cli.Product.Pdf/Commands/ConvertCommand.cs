using System.CommandLine;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class ConvertCommand
{
    public static Command Create(IProductCommandHost<IPdfEngine> host)
    {
        Argument<string> file = PdfOptions.File();
        var to = new Option<string>("--to") { Required = true, Description = "Target PDF export format." }.WithInput(InputKind.None);
        to.AcceptOnlyFromAmong([.. PdfModule.Formats.IdsFor(FormatUse.Convert)]);
        var pages = new Option<string?>("--pages") { Description = "Optional 1-based page range." }.WithInput(InputKind.None);
        var output = new OutputFileOptions("Output path; defaults to a sibling using the target extension.");
        var password = new PasswordOptions("--password", "the PDF");
        var command = new Command("convert", "Convert selected PDF pages to a supported format.");
        command.Arguments.Add(file);
        command.Options.Add(to);
        command.Options.Add(pages);
        output.AddTo(command);
        password.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            string input = context.Paths.ResolveInput(parse.GetRequiredValue(file));
            string format = parse.GetRequiredValue(to);
            string? range = parse.GetValue(pages);
            return context.Port.Convert(input, new PdfConvertRequest
            {
                TargetFormatId = format,
                OutputPath = output.ResolvePath(parse, context.Paths, input, PdfModule.Formats.ExtensionFor(format)),
                Overwrite = output.Overwrite(parse),
                Pages = range is null ? null : PageRange.Parse(range),
                Password = password.Resolve(parse, context.Inputs),
            });
        }));
        return command;
    }
}
