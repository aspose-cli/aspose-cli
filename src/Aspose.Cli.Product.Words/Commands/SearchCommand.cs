using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class SearchCommand
{
    public static CommandDefinition<WordsSearchRequest, WordsSearchResult> Create() =>
        TableCommands.Search<WordsSearchRequest, WordsSearchResult>(
            "Search the text of a document's paragraphs, as Paragraph.GetText() gives it; each hit names the paragraph's address.",
            new CommandTraits { Input = WordsInputs.Document },
            static search => new WordsSearchRequest { Input = search.Input, Password = search.Password, Query = search.Query },
            static (request, result) => result with { Window = SearchOptions.Continue(request.Query, result.Window) },
            examples:
            [
                "words search contract.docx --pattern Contoso",
                "words search contract.docx --pattern \"20[0-9]{2}\" --regex",
            ],
            links:
            [
                CommandHelpLink.Schema(WordsModule.Manifest, "search-result", "the fields of the search result"),
            ]);
}
