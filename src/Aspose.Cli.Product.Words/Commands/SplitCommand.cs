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
        var by = new Option<string>("--by") { Required = true, Description = "section, heading1 (one part per Heading 1, after a leading part for any blocks before the first) or pages." }.WithInput(InputKind.None);
        by.AcceptOnlyFromAmong("section", "heading1", "pages");
        var pages = new Option<string?>("--pages") { Description = "Page range when --by pages." }.WithInput(InputKind.None);
        var outDirectory = new OutputDirectoryOption("Directory that receives the parts.", required: true);
        Option<bool> overwrite = OutputOptions.Overwrite();
        var password = new PasswordOptions("--password", "the document");
        var fonts = new FontDirectoryOptions();

        var command = new Command("split", "Split a document into safe, deterministically named DOCX files.");
        command.Arguments.Add(file);
        command.Options.Add(by);
        command.Options.Add(pages);
        outDirectory.AddTo(command);
        command.Options.Add(overwrite);
        password.AddTo(command);
        fonts.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            string mode = parse.GetRequiredValue(by);
            string? pageText = parse.GetValue(pages);
            if (pageText is not null && mode != "pages")
            {
                throw CliErrors.OptionInvalid("--pages", $"cannot be used with --by {mode}", "Use --by pages or omit the range.");
            }

            string input = context.Paths.ResolveInput(parse.GetRequiredValue(file));
            using IDisposable fontScope = fonts.Use(parse, context);
            return context.Port.Split(
                input,
                new WordsSplitRequest
                {
                    By = mode,
                    Pages = pageText is null ? null : PageRange.Parse(pageText),
                    OutputDirectory = outDirectory.ResolveRequired(parse, context.Paths),
                    Overwrite = parse.GetValue(overwrite),
                    Password = password.Resolve(parse, context.Inputs, context.ReadEnvironment),
                });
        }));
        return command;
    }
}
