using System.CommandLine;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class MergeCommand
{
    public static Command Create(IProductCommandHost<IPdfEngine> host)
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
        return StandardCommand.Create(
            host,
            "merge",
            "Merge PDF inputs in order.",
            new CommandTraits
            {
                PasswordSubject = "all input PDFs",
                Output = OutputTarget.File("Merged PDF output path.", required: true),
            },
            [inputs, bookmarks],
            (parse, standard) =>
            {
                string[] inputPaths = standard.InputFiles(inputs);
                return standard.OpenEngine().Merge(new PdfMergeRequest
                {
                    InputPaths = inputPaths,
                    OutputPath = standard.OutputPath(),
                    Overwrite = standard.Overwrite,
                    PreserveBookmarks = parse.GetRequiredValue(bookmarks) == "preserve",
                    Password = standard.InputPassword,
                });
            });
    }
}
