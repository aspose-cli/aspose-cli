using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class MergeCommand
{
    public static CommandDefinition<PdfMergeRequest, PdfMergeResult> Create()
    {
        var inputs = new Argument<string[]>("files")
        {
            Description = "Two or more PDF inputs in merge order.",
            Arity = ArgumentArity.OneOrMore,
        }.WithInput(InputKind.File);
        var bookmarks = new Option<string>("--bookmarks")
        {
            DefaultValueFactory = _ => "preserve",
            Description = "Whether the merged PDF keeps the bookmarks of its inputs.",
        }.WithInput(InputKind.None);
        bookmarks.AcceptOnlyFromAmong("preserve", "drop");
        return new(
            "merge",
            "Merge PDF inputs in order.",
            new CommandTraits
            {
                PasswordSubject = "all input PDFs",
                Output = OutputTarget.File("Merged PDF output path.", PdfFormats.Document, required: true),
                Encrypt = PdfInputs.EncryptedPdf,
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
                    EncryptPassword = standard.EncryptPassword(),
                };
            },
            static (result, surface) => ResultText.Produced(surface, result.Output, result.Action))
        {
            Examples =
            [
                "pdf merge cover.pdf report.pdf --out combined.pdf",
                "pdf merge part1.pdf part2.pdf part3.pdf --bookmarks drop --out combined.pdf",
            ],
            Links =
            [
                CommandHelpLink.Docs(PdfModule.Manifest, "assemble-review-report", "building a PDF from several sources"),
            ],
        };
    }
}
