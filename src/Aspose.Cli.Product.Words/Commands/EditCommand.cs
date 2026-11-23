using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Words.Commands;

internal static class EditCommand
{
    public static CommandDefinition<TableEditRequest, WordsEditResult> Create() =>
        TableCommands.Edit<WordsEditResult>(
            WordsModule.Table,
            "Apply a batch of set, add, delete, move and do operations to a document; nothing is written when one fails.",
            new CommandTraits
            {
                Input = WordsInputs.Document,
                Encrypt = WordsInputs.EncryptedDocument,
                UsesFonts = true,
            },
            WordsFormats.Editable,
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
