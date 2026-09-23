using System.CommandLine;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class SearchCommand
{
    public static Command Create(IProductCommandHost<IPresentationEngine> host)
    {
        Argument<string> file = SlidesOptions.File();
        var search = new SearchOptions(new SearchScopeGrammar(
            "Search scope: shapes, notes, or all.",
            PresentationSearchScopes.Values,
            PresentationSearchScopes.All));
        var password = new PasswordOptions("--password", "the presentation");
        var command = new Command("search", "Search presentation shape and speaker-notes text.");
        command.Arguments.Add(file);
        search.AddTo(command);
        password.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            SearchQuery query = search.Read(parse);
            return context.Port.Search(
                context.Paths.ResolveInput(parse.GetRequiredValue(file)),
                new PresentationSearchRequest
                {
                    Pattern = query.Text.Pattern,
                    Regex = query.Text.Expression is not null,
                    CaseSensitive = query.Text.CaseSensitive,
                    Scope = query.Scope!,
                    MaxHits = query.MaxHits,
                    Password = password.Resolve(parse, context.Inputs, context.ReadEnvironment),
                });
        }));
        return command;
    }
}
