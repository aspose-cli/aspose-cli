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
        var pattern = new Option<string>("--pattern") { Required = true, Description = "Literal or regex pattern." }.WithInput(InputKind.None);
        var regex = new Option<bool>("--regex") { Description = "Treat the pattern as a regular expression." };
        var caseSensitive = new Option<bool>("--case-sensitive") { Description = "Use case-sensitive matching." };
        var scope = new Option<string>("--scope")
        {
            DefaultValueFactory = _ => PresentationSearchScopes.All,
            Description = "Search scope: shapes, notes, or all.",
        }.WithInput(InputKind.None);
        var maxHits = new Option<int>("--max-hits") { DefaultValueFactory = _ => 100, Description = "Maximum returned hits." };
        var password = new PasswordOptions("--password", "the presentation");
        var command = new Command("search", "Search presentation shape and speaker-notes text.");
        command.Arguments.Add(file);
        command.Options.Add(pattern);
        command.Options.Add(regex);
        command.Options.Add(caseSensitive);
        command.Options.Add(scope);
        command.Options.Add(maxHits);
        password.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
            context.Port.Search(
                context.Paths.ResolveInput(parse.GetRequiredValue(file)),
                new PresentationSearchRequest
                {
                    Pattern = parse.GetRequiredValue(pattern),
                    Regex = parse.GetValue(regex),
                    CaseSensitive = parse.GetValue(caseSensitive),
                    Scope = parse.GetRequiredValue(scope).ToLowerInvariant(),
                    MaxHits = parse.GetValue(maxHits),
                    Password = password.Resolve(parse, context.Inputs, context.ReadEnvironment),
                })));
        return command;
    }
}
