using System.CommandLine;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class SplitCommand
{
    public static Command Create(IProductCommandHost<IWordsEngine> host)
    {
        var by = new Option<string>("--by") { Required = true, Description = "section, heading1 (one part per Heading 1, after a leading part for any blocks before the first) or pages." }.WithInput(InputKind.None);
        by.AcceptOnlyFromAmong("section", "heading1", "pages");
        var pages = new Option<string?>("--pages") { Description = "Page range when --by pages." }.WithInput(InputKind.None);
        return StandardCommand.Create(
            host,
            "split",
            "Split a document into safe, deterministically named DOCX files.",
            new CommandTraits
            {
                Input = WordsCommands.Document,
                Output = OutputTarget.Directory("Directory that receives the parts."),
                UsesFonts = true,
            },
            [by, pages],
            (parse, standard) =>
            {
                string mode = parse.GetRequiredValue(by);
                string? pageText = parse.GetValue(pages);
                if (pageText is not null && mode != "pages")
                {
                    throw CliErrors.OptionInvalid("--pages", $"cannot be used with --by {mode}", "Use --by pages or omit the range.");
                }

                return standard.OpenEngine().Split(standard.Input, new WordsSplitRequest
                {
                    By = mode,
                    Pages = pageText is null ? null : PageRange.Parse(pageText),
                    OutputDirectory = standard.OutputDirectory,
                    Overwrite = standard.Overwrite,
                    Password = standard.InputPassword,
                });
            })
            .WithExamples(
            [
                "words split report.docx --by heading1 --out-dir chapters",
                "words split report.docx --by pages --pages 1-3,8 --out-dir excerpts",
            ]);
    }
}
