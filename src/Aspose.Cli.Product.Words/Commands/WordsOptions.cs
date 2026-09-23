using System.CommandLine;

namespace Aspose.Cli.Product.Words.Commands;

internal static class WordsOptions
{
    public static Argument<string> File(string description = "Document to open.") => new Argument<string>("file")
    {
        Description = description,
    }.WithInput(InputKind.File);
}
