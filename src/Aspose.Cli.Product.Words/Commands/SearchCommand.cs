using System.CommandLine;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class SearchCommand
{
    public static Command Create(IProductCommandHost<IDocumentEngine> host)
    {
        Argument<string> file = WordsOptions.File();
        var search = new SearchOptions(new SearchScopeGrammar(
            "Search scope: body, headers, footnotes, comments or all.",
            ["body", "headers", "footnotes", "comments", "all"],
            "body"));
        var password = new PasswordOptions("--password", "the document");

        var command = new Command("search", "Search bounded document scopes with regex timeout protection.");
        command.Arguments.Add(file);
        search.AddTo(command);
        password.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            SearchQuery query = search.Read(parse);
            return context.Port.Search(
                context.Paths.ResolveInput(parse.GetRequiredValue(file)),
                new WordsSearchRequest
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
