using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Words.Commands;

internal static class ReadCommand
{
    public static CommandDefinition<TableReadRequest, WordsReadResult> Create() =>
        TableCommands.Read<WordsReadResult>(
            WordsModule.Table,
            "Read a document's objects, each with every row of the object table, and its inventory.",
            new CommandTraits { Input = WordsInputs.Document },
            examples:
            [
                "words read contract.docx",
                "words read contract.docx --kind paragraph --max-objects 40",
                "words read contract.docx --at section/0/body/range",
                "words read contract.docx --at section/0/body/table/0 --kind cell --kind paragraph --kind run",
            ],
            links:
            [
                CommandHelpLink.Schema(WordsModule.Manifest, "read-result", "the fields of the read result"),
            ],
            listedWhenNamed: WordsKinds.ListedWhenNamed);
}
