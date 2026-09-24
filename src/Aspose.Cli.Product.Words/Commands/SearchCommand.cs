using System.CommandLine;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class SearchCommand
{
    public static Command Create(IProductCommandHost<IDocumentEngine> host)
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
                return standard.Port.Search(standard.Input, new WordsSearchRequest
                {
                    Pattern = query.Text.Pattern,
                    Regex = query.Text.Expression is not null,
                    CaseSensitive = query.Text.CaseSensitive,
                    Scope = query.Scope!,
                    MaxHits = query.MaxHits,
                    Password = standard.InputPassword,
                });
            })
            .WithExamples(
            [
                "words query search contract.docx --pattern TODO --scope all",
                "words query search contract.docx --pattern \"Section\\s+\\d+\" --regex --max-hits 20",
            ]);
    }
}
