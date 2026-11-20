using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class EditCommand
{
    public static CommandDefinition<WordsEditRequest, WordsEditResult> Create() =>
        TableCommands.Edit<WordsEditRequest, WordsEditResult>(
            WordsModule.Table,
            "Apply a batch of set, add, delete, move and do operations to a document; nothing is written when one fails.",
            new CommandTraits
            {
                Input = WordsInputs.Document,
                Encrypt = WordsInputs.EncryptedDocument,
                UsesFonts = true,
            },
            WordsFormats.Editable,
            static edit => new WordsEditRequest
            {
                Input = edit.Input,
                Batch = edit.Batch,
                Output = edit.Output,
                Password = edit.Password,
                EncryptPassword = edit.EncryptPassword,
            },
            examples:
            [
                "words edit contract.docx --ops ops.json --out revised.docx",
                "words edit contract.docx --in-place --ops ops.json",
            ],
            links:
            [
                CommandHelpLink.Schema(WordsModule.Manifest, "edit-result", "the fields of the edit result"),
            ]);
}
