using System.CommandLine;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class SearchCommand
{
    public static Command Create(IProductCommandHost<IPdfEngine> host)
    {
        Argument<string> file = PdfOptions.File();
        var pattern = new Option<string>("--pattern") { Required = true, Description = "Literal or regex pattern." }.WithInput(InputKind.None);
        var regex = new Option<bool>("--regex") { Description = "Treat the pattern as a regular expression." };
        var caseSensitive = new Option<bool>("--case-sensitive") { Description = "Use case-sensitive matching." };
        var pages = new Option<string?>("--pages") { Description = "Optional 1-based page range." }.WithInput(InputKind.None);
        var maxHits = new Option<int>("--max-hits") { DefaultValueFactory = _ => 100, Description = "Maximum returned hits." };
        var password = new PasswordOptions("--password", "the PDF");
        var command = new Command("search", "Search PDF text and return page rectangles.");
        command.Arguments.Add(file);
        command.Options.Add(pattern);
        command.Options.Add(regex);
        command.Options.Add(caseSensitive);
        command.Options.Add(pages);
        command.Options.Add(maxHits);
        password.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            string? range = parse.GetValue(pages);
            return context.Port.Search(
                context.Paths.ResolveInput(parse.GetRequiredValue(file)),
                new PdfSearchRequest
                {
                    Pattern = parse.GetRequiredValue(pattern),
                    Regex = parse.GetValue(regex),
                    CaseSensitive = parse.GetValue(caseSensitive),
                    Pages = range is null ? null : PageRange.Parse(range),
                    MaxHits = parse.GetValue(maxHits),
                    Password = password.Resolve(parse, context.Inputs, context.ReadEnvironment),
                });
        }));
        return command;
    }
}
