using System.CommandLine;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class CompareCommand
{
    public static Command Create(IProductCommandHost<IDocumentEngine> host)
    {
        var ignoreFormatting = new Option<bool>("--ignore-formatting") { Description = "Ignore formatting-only changes." };
        return StandardCommand.Create(
            host,
            "compare",
            "Semantically compare two documents and optionally save a redline.",
            new CommandTraits
            {
                Input = new InputDocument("Original document.", "the original document", "left"),
                Other = new InputDocument("Changed document.", "the changed document", "right"),
                Output = OutputTarget.File("Optional redline output; its extension selects the format, such as .docx or .pdf."),
                UsesFonts = true,
            },
            [ignoreFormatting],
            (parse, standard) => standard.Port.Compare(standard.Input, standard.Other, new WordsCompareRequest
            {
                IgnoreFormatting = parse.GetValue(ignoreFormatting),
                OutputPath = standard.RequestedOutputPath(),
                Overwrite = standard.Overwrite,
                LeftPassword = standard.InputPassword,
                RightPassword = standard.OtherPassword,
            }))
            .WithExamples(
            [
                "words compare original.docx changed.docx --output json",
                "words compare original.docx changed.docx --out redline.docx",
            ]);
    }
}
