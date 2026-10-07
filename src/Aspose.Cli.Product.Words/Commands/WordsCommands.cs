using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

/// <summary>The complete <c>aspose-cli words</c> product command group.</summary>
internal static class WordsCommands
{
    /// <summary>The document every reading command opens.</summary>
    public static readonly InputDocument Document = new("Document to open.", "the document");

    /// <summary>The password a writing command can put on its document.</summary>
    public static readonly EncryptedOutput EncryptedDocument = new("the output document", WordsFormats.EncryptIds);

    public static Command Create(IProductCommandHost<IWordsEngine> host)
    {
        var words = new Command("words", "Word-processing document automation with layout fidelity.");
        words.Subcommands.Add(InfoCommand.Create(host));
        words.Subcommands.Add(QueryCommand.Create(host));
        words.Subcommands.Add(ConvertCommand.Create(host));
        words.Subcommands.Add(RenderCommand.Create(host));
        words.Subcommands.Add(NewCommand.Create(host));
        words.Subcommands.Add(EditCommand.Create(host));
        words.Subcommands.Add(CompareCommand.Create(host));
        words.Subcommands.Add(SplitCommand.Create(host));
        words.Subcommands.Add(ExtractCommand.Create(host));
        return words.WithExamples(
            [
                "words inspect contract.docx --detail outline sections --preview",
                "words query blocks contract.docx --blocks 1-30 --scope full",
            ],
            [
                CommandHelpLink.Docs(WordsModule.Manifest, "editing", "the document block model and edit operations"),
                CommandHelpLink.Docs(WordsModule.Manifest, "verification", "read-back, semantic and visual verification"),
                CommandHelpLink.Schema(WordsModule.Manifest, "the operation JSON schema"),
            ]);
    }
}
