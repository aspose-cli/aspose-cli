using System.CommandLine;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class ExtractCommand
{
    public static Command Create(IProductCommandHost<IWordsEngine> host)
    {
        var what = new Option<string>("--what") { Required = true, Description = "images, comments or text (the visible block text, one line per paragraph or table row)." }.WithInput(InputKind.None);
        what.AcceptOnlyFromAmong("images", "comments", "text");
        return StandardCommand.Create(
            host,
            "extract",
            "Extract bounded document assets.",
            new CommandTraits
            {
                Input = WordsCommands.Document,
                Output = OutputTarget.Directory("Safe extraction directory."),
            },
            [what],
            (parse, standard) => standard.OpenEngine().Extract(standard.Input, new WordsExtractRequest
            {
                What = parse.GetRequiredValue(what),
                OutputDirectory = standard.OutputDirectory,
                Overwrite = standard.Overwrite,
                Password = standard.InputPassword,
            }))
            .WithExamples(
            [
                "words extract report.docx --what images --out-dir images",
                "words extract report.docx --what text --out-dir text",
            ]);
    }
}
