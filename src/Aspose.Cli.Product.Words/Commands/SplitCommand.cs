using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Words.Commands;

internal static class SplitCommand
{
    public static CommandDefinition<WordsSplitRequest, WordsSplitResult> Create()
    {
        var by = new Option<string>("--by") { Required = true, Description = "section, heading1 (one part per Heading 1, after a leading part for any blocks before the first) or pages." }.WithInput(InputKind.None);
        by.AcceptOnlyFromAmong("section", "heading1", "pages");
        var pages = new PartRangeOption("page", "every page, one part each", onlyWith: "--by pages");
        return new(
            "split",
            "Split a document into safe, deterministically named DOCX files.",
            new CommandTraits
            {
                Input = WordsInputs.Document,
                Output = OutputTarget.Directory("Directory that receives the parts."),
                UsesFonts = true,
            },
            [by, .. pages.Options],
            (parse, standard) =>
            {
                string mode = parse.GetRequiredValue(by);
                string? pageText = pages.Read(parse);
                if (pageText is not null && mode != "pages")
                {
                    throw CliErrors.OptionInvalid(pages.Name, $"cannot be used with --by {mode}", "Use --by pages or omit the range.");
                }

                return new WordsSplitRequest
                {
                    Input = standard.Input,
                    By = mode,
                    Pages = pageText is null ? null : PageRange.Parse(pageText),
                    Output = standard.DirectoryOutput,
                    Password = standard.InputPassword,
                };
            },
            Render)
        {
            Examples =
            [
                "words split report.docx --by heading1 --out-dir chapters",
                "words split report.docx --by pages --pages 1-3,8 --out-dir excerpts",
            ],
        };
    }

    internal static void Render(WordsSplitResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"wrote {result.Outputs.Count} part(s)");
        foreach (SplitOutput output in result.Outputs)
        {
            surface.Out.WriteLine($"  {output.Index}: {output.Output.Path}");
        }
    }
}
