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
        var search = new SearchOptions();
        var pages = new Option<string?>("--pages") { Description = "Optional 1-based page range." }.WithInput(InputKind.None);
        var password = new PasswordOptions("--password", "the PDF");
        var command = new Command("search", "Search PDF text and return page rectangles.");
        command.Arguments.Add(file);
        search.AddTo(command);
        command.Options.Add(pages);
        password.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            SearchQuery query = search.Read(parse);
            string? range = parse.GetValue(pages);
            return context.Port.Search(
                context.Paths.ResolveInput(parse.GetRequiredValue(file)),
                new PdfSearchRequest
                {
                    Pattern = query.Text.Pattern,
                    Regex = query.Text.Expression is not null,
                    CaseSensitive = query.Text.CaseSensitive,
                    Pages = range is null ? null : PageRange.Parse(range),
                    MaxHits = query.MaxHits,
                    Password = password.Resolve(parse, context.Inputs, context.ReadEnvironment),
                });
        }));
        return command;
    }
}
