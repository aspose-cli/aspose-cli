using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class SearchCommand
{
    public static Command Create(IProductCommandHost<IPdfEngine> host)
    {
        var search = new SearchOptions();
        var pages = new Option<string?>("--pages") { Description = "Optional 1-based page range." }.WithInput(InputKind.None);
        return StandardCommand.Create(
            host,
            "search",
            "Search PDF text and return page rectangles.",
            new CommandTraits { Input = PdfCommands.Document },
            [.. search.Options, pages],
            (parse, standard) =>
            {
                SearchQuery query = search.Read(parse);
                string? range = parse.GetValue(pages);
                PdfSearchResult result = standard.OpenEngine().Search(standard.Input, new PdfSearchRequest
                {
                    Query = query,
                    Pages = range is null ? null : PageRange.Parse(range),
                    Password = standard.InputPassword,
                });
                ContinuationCommand resume = standard.Continuation();
                if (range is not null)
                {
                    resume.Option("--pages", range);
                }

                return result with { Window = SearchOptions.Continue(query, result.Window!, resume) };
            });
    }
}
