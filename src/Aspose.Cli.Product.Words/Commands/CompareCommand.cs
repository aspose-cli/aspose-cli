using System.CommandLine;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class CompareCommand
{
    public static Command Create(IProductCommandHost<IDocumentEngine> host)
    {
        var left = new Argument<string>("left") { Description = "Original document." }.WithInput(InputKind.File);
        var right = new Argument<string>("right") { Description = "Changed document." }.WithInput(InputKind.File);
        var outOption = new Option<string?>("--out", "-o") { Description = "Optional redline output; its extension selects the format, such as .docx or .pdf." }.WithInput(InputKind.None);
        Option<bool> overwrite = OutputOptions.Overwrite();
        var ignoreFormatting = new Option<bool>("--ignore-formatting") { Description = "Ignore formatting-only changes." };
        var leftPassword = new PasswordOptions("--left-password", "the original document", allowStdin: false);
        var rightPassword = new PasswordOptions("--right-password", "the changed document", allowStdin: false);
        var fonts = new FontDirectoryOptions();

        var command = new Command("compare", "Semantically compare two documents and optionally save a redline.");
        command.Arguments.Add(left);
        command.Arguments.Add(right);
        command.Options.Add(outOption);
        command.Options.Add(overwrite);
        command.Options.Add(ignoreFormatting);
        leftPassword.AddTo(command);
        rightPassword.AddTo(command);
        fonts.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            string original = context.Paths.ResolveInput(parse.GetRequiredValue(left));
            string changed = context.Paths.ResolveInput(parse.GetRequiredValue(right));
            using IDisposable fontScope = fonts.Use(parse, context);
            return context.Port.Compare(original, changed, new WordsCompareRequest
            {
                IgnoreFormatting = parse.GetValue(ignoreFormatting),
                OutputPath = parse.GetValue(outOption) is { } output ? context.Paths.ResolveOutput(output) : null,
                Overwrite = parse.GetValue(overwrite),
                LeftPassword = leftPassword.Resolve(parse, context.Inputs, context.ReadEnvironment),
                RightPassword = rightPassword.Resolve(parse, context.Inputs, context.ReadEnvironment),
            });
        }));
        return command;
    }
}
