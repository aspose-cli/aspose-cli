using System.CommandLine;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class ExtractCommand
{
    public static Command Create(IProductCommandHost<IDocumentEngine> host)
    {
        Argument<string> file = WordsOptions.File();
        var what = new Option<string>("--what") { Required = true, Description = "images, comments or text (the visible block text, one line per paragraph)." }.WithInput(InputKind.None);
        what.AcceptOnlyFromAmong("images", "comments", "text");
        var outDirectory = new OutputDirectoryOption("Safe extraction directory.", required: true);
        var password = new PasswordOptions("--password", "the document");

        var command = new Command("extract", "Extract bounded document assets.");
        command.Arguments.Add(file);
        command.Options.Add(what);
        outDirectory.AddTo(command);
        password.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
            context.Port.Extract(
                context.Paths.ResolveInput(parse.GetRequiredValue(file)),
                new WordsExtractRequest
                {
                    What = parse.GetRequiredValue(what),
                    OutputDirectory = outDirectory.ResolveRequired(parse, context.Paths),
                    Password = password.Resolve(parse, context.Inputs, context.ReadEnvironment),
                })));
        return command;
    }
}
