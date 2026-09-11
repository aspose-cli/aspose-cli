using System.CommandLine;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class ReadCommand
{
    public static Command Create(IProductCommandHost<IPdfEngine> host)
    {
        Argument<string> file = PdfOptions.File();
        var pages = new Option<string?>("--pages") { Description = "1-based page range, e.g. 1-3,7,9-." }.WithInput(InputKind.None);
        var mode = new Option<string>("--mode")
        {
            Description = "Text projection: plain or layout.",
            DefaultValueFactory = _ => PdfReadModes.Plain,
        }.WithInput(InputKind.None);
        mode.AcceptOnlyFromAmong([.. PdfReadModes.All]);
        var maxChars = new Option<int>("--max-chars")
        {
            Description = "Maximum projected characters.",
            DefaultValueFactory = _ => 20_000,
        };
        var password = new PasswordOptions("--password", "the PDF");
        var command = new Command("pages", "Read a bounded page-text window.");
        command.Arguments.Add(file);
        command.Options.Add(pages);
        command.Options.Add(mode);
        command.Options.Add(maxChars);
        password.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            int characters = parse.GetValue(maxChars);
            OptionGuards.EnsureInRange(
                "--max-chars", characters, 1, 10_000_000,
                "Use a positive bounded character budget.");
            string? range = parse.GetValue(pages);
            return context.Port.Read(
                context.Paths.ResolveInput(parse.GetRequiredValue(file)),
                new PdfReadRequest
                {
                    Pages = range is null ? null : PageRange.Parse(range),
                    Mode = parse.GetValue(mode) ?? PdfReadModes.Plain,
                    MaxCharacters = characters,
                    Password = password.Resolve(parse, context.Inputs),
                });
        }));
        return command;
    }
}
