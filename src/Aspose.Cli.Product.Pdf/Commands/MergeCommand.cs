using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;

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
        return new(
            "merge",
            "Merge PDF inputs in order with the engine's PdfFileEditor.Concatenate.",
            new CommandTraits
            {
                PasswordSubject = "all input PDFs",
                Output = OutputTarget.File("Merged PDF output path.", PdfFormats.Document, required: true),
                Encrypt = PdfInputs.EncryptedPdf,
            },
            [inputs],
            (parse, standard) =>
            {
                string[] inputPaths = standard.InputFiles(inputs);
                return new PdfMergeRequest
                {
                    InputPaths = inputPaths,
                    Output = standard.Output,
                    Password = standard.InputPassword,
                    EncryptPassword = standard.EncryptPassword(),
                };
            })
        {
            Examples =
            [
                "pdf merge cover.pdf report.pdf --out combined.pdf",
                "pdf merge a.pdf b.pdf c.pdf --out combined.pdf --overwrite",
            ],
            Links =
            [
                CommandHelpLink.Schema(PdfModule.Manifest, "merge-result", "the fields of the merge result"),
            ],
        };
    }
}
