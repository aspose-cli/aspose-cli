using Aspose.Cli.Sdk.Extensibility.Output;
using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Product.Words.Commands;

internal static class SearchCommand
{
    public static CommandDefinition<WordsSearchRequest, WordsSearchResult> Create()
    {
        var search = new SearchOptions(new SearchScopeGrammar(
            "Search scope: body (the main text, without the comments and footnotes it anchors), "
                + "headersFooters, footnotes (with endnotes), comments or all.",
            WordsTextScopes.Names,
            WordsTextScopes.Body));
        return new(
            "search",
            "Search bounded document scopes with regex timeout protection.",
            new CommandTraits { Input = WordsInputs.Document },
            search.Options,
            (parse, standard) =>
            {
                SearchQuery query = search.Read(parse);
                return new WordsSearchRequest
                {
                    Input = standard.Input,
                    Query = query,
                    Password = standard.InputPassword,
                };
            },
            Render)
        {
            Finish = static (_, request, result, standard) => result with
            {
                Window = SearchOptions.Continue(request.Query, result.Window, standard.Continuation()),
            },
            Examples =
            [
                "words query search contract.docx --pattern TODO --scope all",
                "words query search contract.docx --pattern \"Section\\s+\\d+\" --regex --max-hits 20",
            ],
        };
    }

    internal static void Render(WordsSearchResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"{result.Hits.Count} hit(s) for '{result.Pattern}'");
        var table = new TextTable("block", "section", "scope", "text");
        foreach (WordsSearchHit hit in result.Hits)
        {
            table.AddRow(
                WordsText.Block(hit.Block),
                TableText.Int(hit.Section),
                hit.Location is { } location ? $"{hit.Scope} ({location}, {hit.Kind})" : hit.Scope,
                hit.Snippet);
        }

        table.WriteTo(surface.Out, surface.Format);
    }
}
