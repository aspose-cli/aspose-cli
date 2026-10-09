using System.CommandLine;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class MergeCommand
{
    public static CommandDefinition<PdfMergeRequest, PdfWriteResult> Create()
    {
        var inputs = new Argument<string[]>("files")
        {
            Description = "Two or more PDF inputs in merge order.",
            Arity = ArgumentArity.OneOrMore,
        }.WithInput(InputKind.File);
        var bookmarks = new Option<string>("--bookmarks")
        {
            DefaultValueFactory = _ => "preserve",
            Description = "preserve or drop input bookmarks.",
        }.WithInput(InputKind.None);
        bookmarks.AcceptOnlyFromAmong("preserve", "drop");
        return new(
            "merge",
            "Merge PDF inputs in order.",
            new CommandTraits
            {
                PasswordSubject = "all input PDFs",
                Output = OutputTarget.File("Merged PDF output path.", PdfFormats.Document, required: true),
            },
            [inputs, bookmarks],
            (parse, standard) =>
            {
                string[] inputPaths = standard.InputFiles(inputs);
                return new PdfMergeRequest
                {
                    InputPaths = inputPaths,
                    Output = standard.Output,
                    PreserveBookmarks = parse.GetRequiredValue(bookmarks) == "preserve",
                    Password = standard.InputPassword,
                };
            },
            PdfText.Written);
    }
}
