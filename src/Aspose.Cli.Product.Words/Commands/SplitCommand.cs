using System.CommandLine;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class SplitCommand
{
    public static Command Create(IProductCommandHost<IDocumentEngine> host)
    {
        Argument<string> file = WordsOptions.File();
        var by = new Option<string>("--by") { Required = true, Description = "section, heading1 or pages." }.WithInput(InputKind.None);
        by.AcceptOnlyFromAmong("section", "heading1", "pages");
        var pages = new Option<string?>("--pages") { Description = "Page range when --by pages." }.WithInput(InputKind.None);
        var outDirectory = new Option<string>("--out-dir", "--out") { Required = true, Description = "Output directory." }.WithInput(InputKind.None);
        Option<bool> overwrite = OutputOptions.Overwrite();
        var password = new PasswordOptions("--password", "the document");

        var command = new Command("split", "Split a document into safe, deterministically named DOCX files.");
        command.Arguments.Add(file);
        command.Options.Add(by);
        command.Options.Add(pages);
        command.Options.Add(outDirectory);
        command.Options.Add(overwrite);
        password.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            string mode = parse.GetRequiredValue(by);
            string? pageText = parse.GetValue(pages);
            if (pageText is not null && mode != "pages")
            {
                throw CliErrors.OptionInvalid("--pages", $"cannot be used with --by {mode}", "Use --by pages or omit the range.");
            }

            return context.Port.Split(
                context.Paths.ResolveInput(parse.GetRequiredValue(file)),
                new WordsSplitRequest
                {
                    By = mode,
                    Pages = pageText is null ? null : PageRange.Parse(pageText),
                    OutputDirectory = WordsOptions.ResolveDirectory(parse, context, outDirectory),
                    Overwrite = parse.GetValue(overwrite),
                    Password = password.Resolve(parse, context.Inputs),
                });
        }));
        return command;
    }
}
