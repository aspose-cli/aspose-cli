using System.CommandLine;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Product.Words.Commands;

internal static class SearchCommand
{
    public static Command Create(IProductCommandHost<IWordsEngine> host)
    {
        var search = new SearchOptions(new SearchScopeGrammar(
            "Search scope: body (the main text, without the comments and footnotes it anchors), "
                + "headersFooters, footnotes (with endnotes), comments or all.",
            WordsTextScopes.Names,
            WordsTextScopes.Body));
        return StandardCommand.Create(
            host,
            "search",
            "Search bounded document scopes with regex timeout protection.",
            new CommandTraits { Input = WordsCommands.Document },
            search.Options,
            (parse, standard) =>
            {
                SearchQuery query = search.Read(parse);
                string input = standard.Input;
                WordsSearchResult result = standard.OpenEngine().Search(input, new WordsSearchRequest
                {
                    Query = query,
                    Password = standard.InputPassword,
                });
                return result with
                {
                    Window = SearchOptions.Continue(query, result.Window!, standard.Continuation()),
                };
            })
            .WithExamples(
            [
                "words query search contract.docx --pattern TODO --scope all",
                "words query search contract.docx --pattern \"Section\\s+\\d+\" --regex --max-hits 20",
            ]);
    }
}
