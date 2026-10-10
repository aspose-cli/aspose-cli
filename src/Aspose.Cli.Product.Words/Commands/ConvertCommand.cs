using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility.Output;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Words.Commands;

internal static class ConvertCommand
{
    public static CommandDefinition<WordsConvertRequest, WordsConvertResult> Create()
    {
        var pages = new PartRangeOption("page", "every page", onlyWith: "fixed-page targets");
        return new(
            "convert",
            "Convert a document using the detected input format.",
            new CommandTraits
            {
                Input = WordsInputs.Document,
                Output = OutputTarget.File("Output path; defaults to a sibling using the target extension."),
                Encrypt = WordsInputs.EncryptedDocument,
                UsesFonts = true,
                Target = TargetFormat.Convert("Target document format.", WordsFormats.Definitions),
            },
            [.. pages.Options],
            (parse, standard) =>
            {
                ResolvedOutput output = standard.Output;
                string format = output.Format.Id;
                string? pageText = pages.Read(parse);
                if (pageText is not null && !WordsFormats.FixedPageFormats.Contains(format, StringComparer.Ordinal))
                {
                    throw CliErrors.OptionInvalid(pages.Name, $"'{format}' is a flow format", $"Use {pages.Name} only with PDF, XPS, OpenXPS, PS or PCL.");
                }

                Secret? encryptPassword = standard.EncryptPassword();
                return new WordsConvertRequest
                {
                    Input = standard.Input,
                    Output = output,
                    Pages = pages.ReadRange(parse),
                    Password = standard.InputPassword,
                    EncryptPassword = encryptPassword,
                };
            },
            Render)
        {
            Examples =
            [
                "words convert contract.docx --to pdf",
                "words convert contract.docx --to pdf --pages 1-3 --out excerpt.pdf",
                "words convert contract.docx --to pdf --font-dir fonts",
            ],
        };
    }

    internal static void Render(WordsConvertResult result, TableSurface surface) =>
        ResultText.Produced(surface, result.Output, result.Pages is null ? null : $"pages {result.Pages}");
}
