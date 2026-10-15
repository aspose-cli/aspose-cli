using System.Globalization;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class SearchCommand
{
    public static CommandDefinition<SlidesSearchRequest, SlidesSearchResult> Create()
    {
        var search = new SearchOptions(new SearchScopeGrammar(
            "Search scope: the shape text, the speaker notes or both.",
            SlidesSearchScopes.Values,
            SlidesSearchScopes.All));
        return new(
            "search",
            "Search presentation shape and speaker-notes text.",
            new CommandTraits { Input = SlidesInputs.Presentation },
            search.Options,
            (parse, standard) => new SlidesSearchRequest
            {
                Input = standard.Input,
                Query = search.Read(parse),
                Password = standard.InputPassword,
            },
            Table)
        {
            MistakenFor = ["query"],
            Examples =
            [
                "slides search deck.pptx --pattern TODO --output-format json",
                "slides search deck.pptx --pattern \"Q[1-4]\" --regex --scope notes",
            ],
            Links =
            [
                CommandHelpLink.Docs(SlidesModule.Manifest, "verification", "slide read-back and visual review"),
                new("aspose-cli docs reading", "reading every product within bounded windows"),
            ],
            Finish = static (_, request, result, standard) =>
                result with { Window = SearchOptions.Continue(request.Query, result.Window, standard.Continuation()) },
        };
    }

    internal static void Table(SlidesSearchResult result, TableSurface surface)
    {
        var table = new TextTable("slide", "id", "scope", "shape", "text");
        foreach (SlidesSearchHit hit in result.Hits)
        {
            table.AddRow(
                TableText.Int(hit.Slide),
                hit.SlideId.ToString(CultureInfo.InvariantCulture),
                hit.Scope,
                hit.ShapeName ?? hit.ShapeId?.ToString(CultureInfo.InvariantCulture),
                hit.Context);
        }

        table.WriteTo(surface.Out, surface.Format);
    }
}
