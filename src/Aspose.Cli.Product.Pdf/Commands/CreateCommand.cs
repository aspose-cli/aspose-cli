using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class CreateCommand
{
    public static CommandDefinition<PdfCreateRequest, PdfCreateResult> Create()
    {
        var template = new Option<string?>("--template") { Description = "A document to save as the new one. Default: the engine's new document, new Document()." }.WithInput(InputKind.File);
        return new(
            "create",
            "Create a document: the engine's new document, or a copy of a template saved in the output's format.",
            new CommandTraits
            {
                Output = OutputTarget.CreatedFile("Path of the document to create, e.g. report.pdf.", PdfFormats.Document),
                Encrypt = PdfInputs.EncryptedPdf,
            },
            [template],
            (parse, standard) => new PdfCreateRequest
            {
                Output = standard.Output,
                Template = standard.Source(template),
                EncryptPassword = standard.EncryptPassword(),
            })
        {
            Examples =
            [
                "pdf create notes.pdf",
                "pdf create letter.pdf --template letterhead.pdf",
            ],
            Links =
            [
                CommandHelpLink.Docs(PdfModule.Manifest, "overview", "what each Pdf command is for"),
            ],
        };
    }
}
