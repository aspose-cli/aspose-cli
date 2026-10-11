using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Words.Commands;

internal static class SplitCommand
{
    public static CommandDefinition<WordsSplitRequest, WordsSplitResult> Create()
    {
        var split = new SplitOptions([SplitBy.Sections, SplitBy.Heading1, SplitBy.Pages]);
        return new(
            "split",
            "Split a document into safe, deterministically named DOCX files.",
            new CommandTraits
            {
                Input = WordsInputs.Document,
                Output = OutputTarget.Directory("Directory that receives the parts, named <stem>.<n>.docx."),
                UsesFonts = true,
            },
            [.. split.Options],
            (parse, standard) => new WordsSplitRequest
            {
                Input = standard.Input,
                Plan = split.Read(parse),
                Output = standard.DirectoryOutput,
                Password = standard.InputPassword,
            },
            Table)
        {
            Examples =
            [
                "words split report.docx --by heading1 --out-dir chapters",
                "words split report.docx --by pages --pages 1-3,8 --out-dir excerpts",
            ],
        };
    }

    internal static void Table(WordsSplitResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"wrote {result.Outputs.Count} part(s)");
        foreach (SplitOutput output in result.Outputs)
        {
            surface.Out.WriteLine($"  {output.Index}: {output.Output.Path}");
        }
    }
}
