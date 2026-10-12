using Aspose.Cli.Sdk.Extensibility;
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
                Output = OutputTarget.Directory("Directory that receives the parts, named <stem>.<n>.docx.", WordsFormats.SplitParts),
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
            Links =
            [
                CommandHelpLink.Docs(WordsModule.Manifest, "overview", "what each Words command is for"),
            ],
        };
    }

    internal static void Table(WordsSplitResult result, TableSurface surface)
    {
        ResultText.Files(surface, "wrote", "part", [.. result.Outputs.Select(static output =>
            new ProducedFile(Holds(output), output.Path, output.Format, output.SizeBytes))]);
    }

    /// <summary>What of the document a part holds, in the unit the split divided by.</summary>
    private static string Holds(WordsPartOutput output) => output switch
    {
        { Section: { } section } => $"section {section}",
        { Pages: { } pages } => $"pages {pages}",
        { Blocks: { } blocks } => $"blocks {blocks}",
        _ => "part",
    };
}
