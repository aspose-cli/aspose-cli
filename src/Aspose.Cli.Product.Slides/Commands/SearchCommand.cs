using System.CommandLine;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class SearchCommand
{
    public static Command Create(IProductCommandHost<IPresentationEngine> host)
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
                return standard.Port.Search(standard.Input, new PresentationSearchRequest
                {
                    Pattern = query.Text.Pattern,
                    Regex = query.Text.Expression is not null,
                    CaseSensitive = query.Text.CaseSensitive,
                    Scope = query.Scope!,
                    MaxHits = query.MaxHits,
                    Password = standard.InputPassword,
                });
            });
    }
}
