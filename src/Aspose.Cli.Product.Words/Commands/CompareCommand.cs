using System.CommandLine;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class CompareCommand
{
    public static Command Create(IProductCommandHost<IWordsEngine> host)
    {
        var ignoreFormatting = new Option<bool>("--ignore-formatting") { Description = "Ignore formatting-only changes." };
        var author = new Option<string?>("--author") { Description = "Author of the redline's revisions; default: Aspose CLI." }.WithInput(InputKind.None);
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
            [ignoreFormatting, author],
            (parse, standard) =>
            {
                string? name = parse.GetValue(author);
                if (name is not null && string.IsNullOrWhiteSpace(name))
                {
                    throw CliErrors.OptionInvalid(
                        "--author",
                        "the author is empty",
                        "Pass the person or agent the redline's revisions are attributed to, or omit --author.");
                }

                return standard.OpenEngine().Compare(standard.Input, standard.Other, new WordsCompareRequest
                {
                    IgnoreFormatting = parse.GetValue(ignoreFormatting),
                    Author = name,
                    OutputPath = standard.RequestedOutputPath(),
                    Overwrite = standard.Overwrite,
                    LeftPassword = standard.InputPassword,
                    RightPassword = standard.OtherPassword,
                });
            })
            .WithExamples(
            [
                "words compare original.docx changed.docx --output json",
                "words compare original.docx changed.docx --out redline.docx --author \"Legal Review\"",
            ]);
    }
}
