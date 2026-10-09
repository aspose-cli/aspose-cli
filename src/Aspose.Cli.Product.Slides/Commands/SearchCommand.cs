using System.Globalization;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class SearchCommand
{
    public static CommandDefinition<PresentationSearchRequest, SlidesSearchResult> Create()
    {
        var search = new SearchOptions(new SearchScopeGrammar(
            "Search scope: shapes, notes, or all.",
            PresentationSearchScopes.Values,
            PresentationSearchScopes.All));
        return new(
            "search",
            "Search presentation shape and speaker-notes text.",
            new CommandTraits { Input = SlidesInputs.Presentation },
            search.Options,
            (parse, standard) => new PresentationSearchRequest
            {
                Input = standard.Input,
                Query = search.Read(parse),
                Password = standard.InputPassword,
            },
            Table)
        {
            Finish = static (_, request, result, standard) =>
                result with { Window = SearchOptions.Continue(request.Query, result.Window!, standard.Continuation()) },
        };
    }

    internal static void Table(SlidesSearchResult result, TableSurface surface)
    {
        var table = new TextTable("slide", "id", "scope", "shape", "start", "text");
        foreach (SlidesSearchHit hit in result.Hits)
        {
            table.AddRow(
                TableText.Int(hit.Slide),
                hit.SlideId.ToString(CultureInfo.InvariantCulture),
                hit.Scope,
                hit.ShapeName ?? hit.ShapeId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                TableText.Int(hit.Start),
                hit.Text);
        }

        table.WriteTo(surface.Out, surface.Format);
    }
}
