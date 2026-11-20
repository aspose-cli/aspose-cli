using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class ReadCommand
{
    public static CommandDefinition<WordsReadRequest, WordsReadResult> Create() =>
        TableCommands.Read<WordsReadRequest, WordsReadResult>(
            WordsModule.Table,
            "Read a document's objects, each with every row of the object table, and its inventory.",
            new CommandTraits { Input = WordsInputs.Document },
            static read => new WordsReadRequest
            {
                Input = read.Input,
                Password = read.Password,
                At = read.At,
                From = read.From,
                Kinds = read.Kinds,
                MaxObjects = read.MaxObjects,
            },
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
