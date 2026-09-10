using System.CommandLine;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class WordsOptions
{
    public static Argument<string> File(string description = "Document to open.") => new("file")
    {
        Description = description,
    };

    public static string ResolveDirectory(
        ParseResult parseResult,
        ProductCommandContext<IDocumentEngine> context,
        Option<string> option)
    {
        string path = context.Paths.ResolveOutput(parseResult.GetRequiredValue(option));
        if (System.IO.File.Exists(path))
        {
            throw CliErrors.OptionInvalid(option.Name, $"a file already exists at '{path}'", "Pass a directory path.");
        }

        return path;
    }
}
