using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class CreateCommand
{
    public static CommandDefinition<WordsCreateRequest, WordsCreateResult> Create()
    {
        var template = new Option<string?>("--template") { Description = "A document to save as the new one. Default: the engine's new document, new Document()." }.WithInput(InputKind.File);
        return new(
            "create",
            "Create a document: the engine's new document, or a copy of a template saved in the output's format.",
            new CommandTraits
            {
                Output = OutputTarget.CreatedFile("Path of the document to create, e.g. report.docx.", WordsFormats.Editable),
                Encrypt = WordsInputs.EncryptedDocument,
            },
            [template],
            (parse, standard) => new WordsCreateRequest
            {
                Output = standard.Output,
                Template = standard.Source(template),
                EncryptPassword = standard.EncryptPassword(),
            })
        {
            Examples =
            [
                "words create notes.docx",
                "words create letter.docx --template letter.dotx",
            ],
            Links =
            [
                CommandHelpLink.Schema(WordsModule.Manifest, "create-result", "the fields of the create result"),
            ],
        };
    }
}
