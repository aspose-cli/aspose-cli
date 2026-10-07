using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class ConvertCommand
{
    public static Command Create(IProductCommandHost<IWordsEngine> host)
    {
        var pages = new Option<string?>("--pages") { Description = "1-based pages for fixed-page targets only." }.WithInput(InputKind.None);
        return StandardCommand.Create(
            host,
            "convert",
            "Convert a document using the detected input format.",
            new CommandTraits
            {
                Input = WordsCommands.Document,
                Output = OutputTarget.File("Output path; defaults to a sibling using the target extension."),
                Encrypt = WordsCommands.EncryptedDocument,
                UsesFonts = true,
                Target = TargetFormat.Convert("Target document format.", WordsFormats.Definitions),
            },
            [pages],
            (parse, standard) =>
            {
                string format = standard.TargetFormat();
                string? pageText = parse.GetValue(pages);
                if (pageText is not null && !WordsFormats.FixedPageConvertIds.Contains(format, StringComparer.Ordinal))
                {
                    throw CliErrors.OptionInvalid("--pages", $"'{format}' is a flow format", "Use --pages only with PDF, XPS, OpenXPS, PS or PCL.");
                }

                string? encryptPassword = standard.EncryptPassword(format);
                return standard.OpenEngine().Convert(standard.Input, new WordsConvertRequest
                {
                    TargetFormatId = format,
                    OutputPath = standard.OutputPath(WordsFormats.Definitions.ExtensionFor(format)),
                    Overwrite = standard.Overwrite,
                    Pages = pageText is null ? null : PageRange.Parse(pageText),
                    Password = standard.InputPassword,
                    EncryptPassword = encryptPassword,
                });
            })
            .WithExamples(
            [
                "words convert contract.docx --to pdf",
                "words convert contract.docx --to pdf --pages 1-3 --out excerpt.pdf",
                "words convert contract.docx --to pdf --font-dir fonts",
            ]);
    }
}
