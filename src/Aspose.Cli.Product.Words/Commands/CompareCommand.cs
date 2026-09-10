using System.CommandLine;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class CompareCommand
{
    public static Command Create(IProductCommandHost<IDocumentEngine> host)
    {
        var left = new Argument<string>("left") { Description = "Original document." };
        var right = new Argument<string>("right") { Description = "Changed document." };
        var outOption = new Option<string?>("--out", "-o") { Description = "Optional DOCX redline output." };
        Option<bool> overwrite = OutputOptions.Overwrite();
        var ignoreFormatting = new Option<bool>("--ignore-formatting") { Description = "Ignore formatting-only changes." };
        var leftPassword = new PasswordOptions("--left-password", "the original document", allowStdin: false);
        var rightPassword = new PasswordOptions("--right-password", "the changed document", allowStdin: false);

        var command = new Command("compare", "Semantically compare two documents and optionally save a redline.");
        command.Arguments.Add(left);
        command.Arguments.Add(right);
        command.Options.Add(outOption);
        command.Options.Add(overwrite);
        command.Options.Add(ignoreFormatting);
        leftPassword.AddTo(command);
        rightPassword.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
            context.Port.Compare(
                context.Paths.ResolveInput(parse.GetRequiredValue(left)),
                context.Paths.ResolveInput(parse.GetRequiredValue(right)),
                new WordsCompareRequest
                {
                    IgnoreFormatting = parse.GetValue(ignoreFormatting),
                    OutputPath = parse.GetValue(outOption) is { } output ? context.Paths.ResolveOutput(output) : null,
                    Overwrite = parse.GetValue(overwrite),
                    LeftPassword = leftPassword.Resolve(parse, context.Inputs),
                    RightPassword = rightPassword.Resolve(parse, context.Inputs),
                })));
        return command;
    }
}
