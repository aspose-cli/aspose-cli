using System.CommandLine;
using System.Text;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Words.Commands;

/// <summary>
/// <c>words convert</c>: a document in any format the engine reads, saved in another with the
/// engine's options for it. How a plain text or Markdown input is read is stated by the caller:
/// the engine would otherwise detect it.
/// </summary>
internal static class ConvertCommand
{
    private const string Encoding = "--encoding";
    private const string AutoNumbering = "--auto-numbering-detection";
    private const string NumberingWithWhitespaces = "--detect-numbering-with-whitespaces";
    private const string Hyperlinks = "--detect-hyperlinks";
    private const string LeadingSpaces = "--leading-spaces";

    public static CommandDefinition<WordsConvertRequest, WordsConvertResult> Create()
    {
        IReadOnlyList<string> fixedPage = WordsFormats.FixedPageFormats;
        string fixedPageTargets = $"--to {string.Join(", ", fixedPage.Take(fixedPage.Count - 1))} or {fixedPage[^1]}";
        var pages = new PartRangeOption("page", "every page", onlyWith: fixedPageTargets, formats: fixedPage);
        var encoding = new Option<string?>(Encoding) { Description = "The text encoding of a txt or md input, such as utf-8, gb18030 or windows-1252 (LoadOptions.Encoding). Required for such an input." }.WithInput(InputKind.None);
        var numbering = new Option<bool?>(AutoNumbering) { Description = "true or false: TxtLoadOptions.AutoNumberingDetection of a txt input. Required for such an input." }.WithInput(InputKind.None);
        var whitespaces = new Option<bool?>(NumberingWithWhitespaces) { Description = "true or false: TxtLoadOptions.DetectNumberingWithWhitespaces of a txt input. Required for such an input." }.WithInput(InputKind.None);
        var hyperlinks = new Option<bool?>(Hyperlinks) { Description = "true or false: TxtLoadOptions.DetectHyperlinks of a txt input. Required for such an input." }.WithInput(InputKind.None);
        var leading = new Option<string?>(LeadingSpaces) { Description = "TxtLoadOptions.LeadingSpacesOptions of a txt input, a member name of the engine's TxtLeadingSpacesOptions, such as ConvertToIndent, Preserve or Trim. Required for such an input." }.WithInput(InputKind.None);
        return new(
            "convert",
            "Convert a document to another format with the engine's options for it. An html, html-fixed or md output is one file: it holds its images, fonts and styles itself.",
            new CommandTraits
            {
                Input = WordsInputs.Document,
                Output = OutputTarget.File("Output path; defaults to a sibling using the target extension."),
                Encrypt = WordsInputs.EncryptedDocument,
                UsesFonts = true,
                Target = TargetFormat.Convert("Target document format.", WordsFormats.Definitions),
            },
            [.. pages.Options, encoding, numbering, whitespaces, hyperlinks, leading],
            (parse, standard) =>
            {
                ResolvedOutput output = standard.Output;
                string format = output.Format.Id;
                if (pages.Read(parse) is not null && !pages.AppliesTo(format))
                {
                    throw CliErrors.OptionInvalid(pages.Name, $"'{format}' output holds the whole document; it applies only with {fixedPageTargets}");
                }

                PageRange? range = pages.ReadRange(parse);
                WordsTextInput? textInput = TextInput(standard.Input, parse, encoding, numbering, whitespaces, hyperlinks, leading);
                return new WordsConvertRequest
                {
                    Input = standard.Input,
                    Output = output,
                    Pages = range,
                    Password = standard.InputPassword,
                    EncryptPassword = standard.EncryptPassword(),
                    TextInput = textInput,
                };
            })
        {
            Examples =
            [
                "words convert contract.docx --to pdf",
                "words convert contract.docx --to pdf --pages 1-3 --out excerpt.pdf",
                "words convert notes.md --to docx --encoding utf-8",
                "words convert notes.txt --to docx --encoding utf-8 --auto-numbering-detection false --detect-numbering-with-whitespaces false --detect-hyperlinks false --leading-spaces Preserve",
            ],
            Links =
            [
                CommandHelpLink.Schema(WordsModule.Manifest, "convert-result", "the fields of the convert result"),
            ],
        };
    }

    /// <summary>How a txt or md input is read: every option stated, by the input's extension; null for any other input.</summary>
    private static WordsTextInput? TextInput(
        string input, ParseResult parse, Option<string?> encoding, Option<bool?> numbering, Option<bool?> whitespaces, Option<bool?> hyperlinks, Option<string?> leading)
    {
        string? format = WordsFormats.Definitions.NamedBy(input).FirstOrDefault(static candidate => WordsFormats.IsText(candidate.Id))?.Id;
        bool txt = format is not null && WordsFormats.IsPlainText(format);
        (string Name, bool Given)[] options =
        [
            (Encoding, parse.GetValue(encoding) is not null),
            (AutoNumbering, parse.GetValue(numbering) is not null),
            (NumberingWithWhitespaces, parse.GetValue(whitespaces) is not null),
            (Hyperlinks, parse.GetValue(hyperlinks) is not null),
            (LeadingSpaces, parse.GetValue(leading) is not null),
        ];
        foreach ((string name, bool given) in options)
        {
            bool applies = txt || (format is not null && WordsFormats.IsMarkdown(format) && name == Encoding);
            if (given && !applies)
            {
                throw CliErrors.OptionInvalid(name, format is null ? "the input is no txt or md text" : $"a {format} input does not take it");
            }

            if (!given && applies)
            {
                throw CliErrors.OptionInvalid(name, $"a {format} input is read with it stated: the engine would otherwise detect it");
            }
        }

        if (format is null)
        {
            return null;
        }

        string encodingName = parse.GetValue(encoding)!;
        System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        System.Text.Encoding text;
        try
        {
            text = System.Text.Encoding.GetEncoding(encodingName);
        }
        catch (ArgumentException)
        {
            throw CliErrors.OptionInvalid(Encoding, $"'{encodingName}' is no encoding name .NET knows, such as utf-8, gb18030 or windows-1252");
        }

        return txt
            ? new WordsTextInput(text, parse.GetValue(numbering), parse.GetValue(whitespaces), parse.GetValue(hyperlinks), parse.GetValue(leading))
            : new WordsTextInput(text);
    }
}
