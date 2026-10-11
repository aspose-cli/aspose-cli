using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Words.Commands;

internal static class ExtractCommand
{
    public static CommandDefinition<WordsExtractRequest, WordsExtractResult> Create()
    {
        var what = new Option<string>("--what") { Required = true, Description = "What to extract; tables writes one CSV file per body table." }.WithInput(InputKind.None);
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
            Table)
        {
            Examples =
            [
                "words extract report.docx --what images --out-dir images",
                "words extract report.docx --what text --out-dir text",
                "words extract report.docx --what tables --out-dir tables",
            ],
            Links =
            [
                CommandHelpLink.Docs(WordsModule.Manifest, "overview", "what each Words command is for"),
            ],
        };
    }

    internal static void Table(WordsExtractResult result, TableSurface surface)
    {
        ResultText.Files(surface, "extracted", $"{result.What} item", [.. result.Items.Select(static item =>
            new ProducedFile(null, item.Path, null, item.SizeBytes))]);
    }
}
