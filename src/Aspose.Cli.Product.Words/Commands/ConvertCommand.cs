using System.CommandLine;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class ConvertCommand
{
    public static Command Create(IProductCommandHost<IDocumentEngine> host)
    {
        var to = new Option<string>("--to") { Required = true, Description = "Target document format." }.WithInput(InputKind.None);
        to.AcceptOnlyFromAmong([.. WordsFormats.Definitions.IdsFor(FormatUse.Convert)]);
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
            },
            [to, pages],
            (parse, standard) =>
            {
                string format = parse.GetRequiredValue(to);
                string? pageText = parse.GetValue(pages);
                if (pageText is not null && !WordsFormats.FixedPageConvertIds.Contains(format, StringComparer.Ordinal))
                {
                    throw CliErrors.OptionInvalid("--pages", $"'{format}' is a flow format", "Use --pages only with PDF, XPS, OpenXPS, PS or PCL.");
                }

                return standard.Port.Convert(standard.Input, new WordsConvertRequest
                {
                    TargetFormatId = format,
                    OutputPath = standard.OutputPath(WordsFormats.Definitions.ExtensionFor(format)),
                    Overwrite = standard.Overwrite,
                    Pages = pageText is null ? null : PageRange.Parse(pageText),
                    Password = standard.InputPassword,
                    EncryptPassword = standard.EncryptPassword(format),
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
