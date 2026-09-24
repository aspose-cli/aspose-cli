using System.CommandLine;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Extensibility;

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
                return standard.OpenEngine().Search(standard.Input, new PdfSearchRequest
                {
                    Pattern = query.Text.Pattern,
                    Regex = query.Text.Expression is not null,
                    CaseSensitive = query.Text.CaseSensitive,
                    Pages = range is null ? null : PageRange.Parse(range),
                    MaxHits = query.MaxHits,
                    Password = standard.InputPassword,
                });
            });
    }
}
