using System.CommandLine;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

/// <summary>The complete <c>aspose-cli words</c> product command group.</summary>
internal static class WordsCommands
{
    public static Command Create(IProductCommandHost<IDocumentEngine> host)
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
        WordsHelpMetadata.Attach(words);
        return words;
    }
}
