using System.CommandLine;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class InfoCommand
{
    private static readonly string[] Details =
        ["outline", "sections", "styles", "fields", "bookmarks", "comments", "images", "tables", "properties", "fonts"];

    public static Command Create(IProductCommandHost<IDocumentEngine> host)
    {
        Argument<string> file = WordsOptions.File();
        var preview = new Option<bool>("--preview") { Description = "Include a bounded outline preview." };
        var detail = new Option<string[]>("--detail")
        {
            Description = "Extra structural projections; repeatable.",
            AllowMultipleArgumentsPerToken = true,
        };
        detail.AcceptOnlyFromAmong(Details);
        var password = new PasswordOptions("--password", "the document");

        var command = new Command("inspect", "Show document structure, safety state and metadata.");
        command.Arguments.Add(file);
        command.Options.Add(preview);
        command.Options.Add(detail);
        password.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
            context.Port.GetInfo(
                context.Paths.ResolveInput(parse.GetRequiredValue(file)),
                new DocumentInfoRequest
                {
                    IncludePreview = parse.GetValue(preview),
                    Details = parse.GetValue(detail),
                    Password = password.Resolve(parse, context.Inputs),
                })));
        return command;
    }
}
