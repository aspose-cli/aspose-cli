using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class SearchCommand
{
    public static CommandDefinition<TableSearchRequest, SlidesSearchResult> Create() =>
        TableCommands.Search<SlidesSearchResult>(
            "Search the text of a presentation's shapes, table cells, notes and chart titles; each hit names the object's address.",
            new CommandTraits { Input = SlidesInputs.Presentation },
            examples:
            [
                "slides search deck.pptx --pattern TODO",
                "slides search deck.pptx --pattern \"Q[1-4]\" --regex",
            ],
            links:
            [
                CommandHelpLink.Schema(SlidesModule.Manifest, "search-result", "the fields of the search result"),
            ]);
}
