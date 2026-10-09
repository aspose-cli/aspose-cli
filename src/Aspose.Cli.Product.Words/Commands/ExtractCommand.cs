using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Words.Commands;

internal static class ExtractCommand
{
    public static CommandDefinition<WordsExtractRequest, WordsExtractResult> Create()
    {
        var what = new Option<string>("--what") { Required = true, Description = "images, comments, text (the visible block text, one line per paragraph or table row) or tables (one CSV file per body table)." }.WithInput(InputKind.None);
        what.AcceptOnlyFromAmong([.. WordsExtractTargets.Names]);
        return new(
            "extract",
            "Extract bounded document assets.",
            new CommandTraits
            {
                Input = WordsInputs.Document,
                Output = OutputTarget.Directory("Safe extraction directory."),
            },
            [what],
            (parse, standard) => new WordsExtractRequest
            {
                Input = standard.Input,
                What = parse.GetRequiredValue(what),
                Output = standard.DirectoryOutput,
                Password = standard.InputPassword,
            },
            Render)
        {
            Examples =
            [
                "words extract report.docx --what images --out-dir images",
                "words extract report.docx --what text --out-dir text",
                "words extract report.docx --what tables --out-dir tables",
            ],
        };
    }

    internal static void Render(WordsExtractResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"extracted {result.Items.Count} {result.What} item(s)");
        foreach (ExtractedItem item in result.Items)
        {
            surface.Out.WriteLine($"  {item.Path} ({TableText.Bytes(item.SizeBytes)})");
        }
    }
}
