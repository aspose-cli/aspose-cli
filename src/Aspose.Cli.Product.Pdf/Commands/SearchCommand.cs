using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility.Output;
using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class SearchCommand
{
    public static CommandDefinition<PdfSearchRequest, PdfSearchResult> Create()
    {
        var search = new SearchOptions();
        var pages = new Option<string?>("--pages") { Description = "Optional 1-based page range." }.WithInput(InputKind.None);
        return new(
            "search",
            "Search PDF text and return page rectangles.",
            new CommandTraits { Input = PdfInputs.Document },
            [.. search.Options, pages],
            (parse, standard) =>
            {
                SearchQuery query = search.Read(parse);
                string? range = parse.GetValue(pages);
                return new PdfSearchRequest
                {
                    Input = standard.Input,
                    Query = query,
                    Pages = range is null ? null : PageRange.Parse(range),
                    Password = standard.InputPassword,
                };
            },
            Table)
        {
            Finish = (parse, request, result, standard) =>
            {
                ContinuationCommand resume = standard.Continuation();
                if (parse.GetValue(pages) is { } range)
                {
                    resume.Option("--pages", range);
                }

                return result with { Window = SearchOptions.Continue(request.Query, result.Window, resume) };
            },
        };
    }

    internal static void Table(PdfSearchResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"found {result.Hits.Count} hit(s)");
        var table = new TextTable("page", "occurrence", "rectangle", "text");
        foreach (PdfSearchHit hit in result.Hits)
        {
            table.AddRow(
                TableText.Int(hit.Page),
                TableText.Int(hit.Occurrence),
                PdfText.Rectangle(hit.Rect),
                hit.Context ?? hit.Snippet);
        }

        table.WriteTo(surface.Out, surface.Format);
    }
}
