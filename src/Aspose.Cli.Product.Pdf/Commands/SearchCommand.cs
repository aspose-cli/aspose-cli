using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Output;
using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class SearchCommand
{
    public static CommandDefinition<PdfSearchRequest, PdfSearchResult> Create()
    {
        var search = new SearchOptions();
        var pages = new PartRangeOption("page", "every page");
        return new(
            "search",
            "Search PDF text and return page rectangles.",
            new CommandTraits { Input = PdfInputs.Document },
            [.. search.Options, .. pages.Options],
            (parse, standard) =>
            {
                SearchQuery query = search.Read(parse);
                return new PdfSearchRequest
                {
                    Input = standard.Input,
                    Query = query,
                    Pages = pages.ReadRange(parse),
                    Password = standard.InputPassword,
                };
            },
            Table)
        {
            MistakenFor = ["query"],
            Finish = (parse, request, result, standard) =>
            {
                ContinuationCommand resume = standard.Continuation();
                if (pages.Read(parse) is { } range)
                {
                    resume.Option(pages.Name, range);
                }

                return result with { Window = SearchOptions.Continue(request.Query, result.Window, resume) };
            },
            Examples =
            [
                "pdf search report.pdf --pattern invoice --output-format json",
                "pdf search report.pdf --pattern \"total: [0-9]+\" --regex --pages 2-4",
            ],
            Links =
            [
                CommandHelpLink.Docs(PdfModule.Manifest, "overview", "what each PDF command is for"),
                new("aspose-cli docs reading", "reading every product within bounded windows"),
            ],
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
                hit.Context ?? hit.Text);
        }

        table.WriteTo(surface.Out, surface.Format);
    }
}
