using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Words.Commands;

internal static class SearchCommand
{
    public static CommandDefinition<TableSearchRequest, WordsSearchResult> Create() =>
        TableCommands.Search<WordsSearchResult>(
            "Search the text of a document's paragraphs, as Paragraph.GetText() gives it; each hit names the paragraph's address.",
            new CommandTraits { Input = WordsInputs.Document },
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
