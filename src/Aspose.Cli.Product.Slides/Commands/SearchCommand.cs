using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class SearchCommand
{
    public static Command Create(IProductCommandHost<ISlidesEngine> host)
    {
        var search = new SearchOptions(new SearchScopeGrammar(
            "Search scope: shapes, notes, or all.",
            PresentationSearchScopes.Values,
            PresentationSearchScopes.All));
        return StandardCommand.Create(
            host,
            "search",
            "Search presentation shape and speaker-notes text.",
            new CommandTraits { Input = SlidesCommands.Presentation },
            search.Options,
            (parse, standard) =>
            {
                SearchQuery query = search.Read(parse);
                SlidesSearchResult result = standard.OpenEngine().Search(standard.Input, new PresentationSearchRequest
                {
                    Query = query,
                    Password = standard.InputPassword,
                });
                return result with
                {
                    Window = SearchOptions.Continue(query, result.Window!, standard.Continuation()),
                };
            });
    }
}
