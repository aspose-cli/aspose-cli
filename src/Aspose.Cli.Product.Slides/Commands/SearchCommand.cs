using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class SearchCommand
{
    public static CommandDefinition<SlidesSearchRequest, SlidesSearchResult> Create() =>
        TableCommands.Search<SlidesSearchRequest, SlidesSearchResult>(
            "Search the text of a presentation's shapes, table cells, notes and chart titles; each hit names the object's address.",
            new CommandTraits { Input = SlidesInputs.Presentation },
            static search => new SlidesSearchRequest { Input = search.Input, Password = search.Password, Query = search.Query },
            static (request, result) => result with { Window = SearchOptions.Continue(request.Query, result.Window) },
            examples:
            [
                "slides search deck.pptx --pattern TODO",
                "slides search deck.pptx --pattern \"Q[1-4]\" --regex",
            ],
            links:
            [
                CommandHelpLink.Docs(SlidesModule.Manifest, "overview", "what each Slides command is for"),
            ]);
}
