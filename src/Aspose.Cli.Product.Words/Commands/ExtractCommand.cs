using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class ExtractCommand
{
    public static CommandDefinition<WordsExtractRequest, WordsExtractResult> Create()
    {
        var what = new Option<string>("--what") { Required = true, Description = "What to extract: images, the bytes of each image the document stores." }.WithInput(InputKind.None);
        what.AcceptOnlyFromAmong([.. WordsExtractKinds.All]);
        return new(
            "extract",
            "Write the bytes of each image the document stores, ImageData.ImageBytes, one file per image in document order; a linked image stores none.",
            new CommandTraits
            {
                Input = WordsInputs.Document,
                Output = OutputTarget.Directory("The directory the images go to."),
            },
            [what],
            (parse, standard) => new WordsExtractRequest
            {
                Input = standard.Input,
                What = parse.GetRequiredValue(what),
                Output = standard.DirectoryOutput,
                Password = standard.InputPassword,
            })
        {
            Examples =
            [
                "words extract report.docx --what images --out-dir images",
                "words extract locked.docx --what images --password-env DOC_PASSWORD --out-dir images",
            ],
            Links =
            [
                CommandHelpLink.Schema(WordsModule.Manifest, "extract-result", "the fields of the extract result"),
            ],
        };
    }
}
