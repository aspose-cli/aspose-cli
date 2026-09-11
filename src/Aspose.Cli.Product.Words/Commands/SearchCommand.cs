using System.CommandLine;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class SearchCommand
{
    public static Command Create(IProductCommandHost<IDocumentEngine> host)
    {
        Argument<string> file = WordsOptions.File();
        var pattern = new Option<string>("--pattern") { Required = true, Description = "Literal or regex pattern." }.WithInput(InputKind.None);
        var regex = new Option<bool>("--regex") { Description = "Treat the pattern as a regular expression." };
        var caseSensitive = new Option<bool>("--case-sensitive") { Description = "Use ordinal case-sensitive matching." };
        var scope = new Option<string>("--scope") { DefaultValueFactory = _ => "body", Description = "body, headers, footnotes, comments or all." }.WithInput(InputKind.None);
        scope.AcceptOnlyFromAmong("body", "headers", "footnotes", "comments", "all");
        var maxHits = new Option<int>("--max-hits") { DefaultValueFactory = _ => 100, Description = "Maximum returned hits." };
        var password = new PasswordOptions("--password", "the document");

        var command = new Command("search", "Search bounded document scopes with regex timeout protection.");
        command.Arguments.Add(file);
        command.Options.Add(pattern);
        command.Options.Add(regex);
        command.Options.Add(caseSensitive);
        command.Options.Add(scope);
        command.Options.Add(maxHits);
        password.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            int limit = parse.GetValue(maxHits);
            OptionGuards.EnsureInRange("--max-hits", limit, 1, 100_000, "Use a positive bounded result limit.");
            return context.Port.Search(
                context.Paths.ResolveInput(parse.GetRequiredValue(file)),
                new WordsSearchRequest
                {
                    Pattern = parse.GetRequiredValue(pattern),
                    Regex = parse.GetValue(regex),
                    CaseSensitive = parse.GetValue(caseSensitive),
                    Scope = parse.GetValue(scope) ?? "body",
                    MaxHits = limit,
                    Password = password.Resolve(parse, context.Inputs),
                });
        }));
        return command;
    }
}
