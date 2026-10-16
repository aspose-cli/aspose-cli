using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Output;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Words.Commands;

internal static class ConvertCommand
{
    public static CommandDefinition<WordsConvertRequest, WordsConvertResult> Create()
    {
        IReadOnlyList<string> fixedPage = WordsFormats.FixedPageFormats;
        string fixedPageTargets = $"--to {string.Join(", ", fixedPage.Take(fixedPage.Count - 1))} or {fixedPage[^1]}";
        var pages = new PartRangeOption("page", "every page", onlyWith: fixedPageTargets);
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
                    throw CliErrors.OptionInvalid(pages.Name, $"a '{format}' output holds the whole document", $"Use {pages.Name} only with {fixedPageTargets}.");
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
            Table)
        {
            Examples =
            [
                "words convert contract.docx --to pdf",
                "words convert contract.docx --to pdf --pages 1-3 --out excerpt.pdf",
                "words convert contract.docx --to pdf --font-dir fonts",
            ],
            Links =
            [
                CommandHelpLink.Docs(WordsModule.Manifest, "overview", "what each Words command is for"),
            ],
        };
    }

    internal static void Table(WordsConvertResult result, TableSurface surface) =>
        ResultText.Files(surface, "wrote", "file", [.. result.Outputs.Select(output =>
            new ProducedFile(null, output.Path, output.Format, output.SizeBytes, result.Pages is null ? null : $"pages {result.Pages}"))]);
}
