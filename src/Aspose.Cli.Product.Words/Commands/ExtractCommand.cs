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
        var bom = new BomOption("each table's CSV", onlyWith: "--what tables");
        return new(
            "extract",
            "Extract bounded document assets.",
            new CommandTraits
            {
                Input = WordsInputs.Document,
                Output = OutputTarget.Directory("Safe extraction directory."),
            },
            [what, .. bom.Options],
            (parse, standard) =>
            {
                // The usage check runs before the input is resolved or read.
                string kind = parse.GetRequiredValue(what);
                bool byteOrderMark = bom.Read(parse, kind == WordsExtractTargets.Tables ? null : $"--what {kind} writes no CSV tables");

                return new WordsExtractRequest
                {
                    Input = standard.Input,
                    What = kind,
                    Output = standard.DirectoryOutput,
                    ByteOrderMark = byteOrderMark,
                    Password = standard.InputPassword,
                };
            },
            Table)
        {
            Examples =
            [
                "words extract report.docx --what images --out-dir images",
                "words extract report.docx --what text --out-dir text",
                "words extract report.docx --what tables --bom --out-dir tables",
            ],
            Links =
            [
                CommandHelpLink.Docs(WordsModule.Manifest, "overview", "what each Words command is for"),
            ],
        };
    }

    internal static void Table(WordsExtractResult result, TableSurface surface)
    {
        ResultText.Files(surface, "extracted", $"{result.What} item", [.. result.Outputs.Select(static item =>
            new ProducedFile(item.Block is { } block ? $"block {block}" : null, item.Path, item.Format, item.SizeBytes))]);
    }
}
