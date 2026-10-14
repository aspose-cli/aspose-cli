using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Output;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Words.Commands;

internal static class CreateCommand
{
    public static CommandDefinition<WordsCreateRequest, WordsCreateResult> Create()
    {
        var markdown = new Option<string?>("--from-markdown") { Description = "Create from a Markdown file." }.WithInput(InputKind.File);
        var text = new Option<string?>("--from-text") { Description = "Create from a UTF-8 text file." }.WithInput(InputKind.File);
        var template = new Option<string?>("--template") { Description = "Document whose styles, page setup, headers and footers the new document uses; its body is replaced by --from-markdown or --from-text content." }.WithInput(InputKind.File);
        var title = new Option<string?>("--title") { Description = "Set the built-in title property." }.WithInput(InputKind.None);
        return new(
            "create",
            "Create a document from at most one content source, optionally inside a template; without either it is blank.",
            new CommandTraits
            {
                Output = OutputTarget.CreatedFile("Document path to create.", WordsFormats.Editable),
                Encrypt = WordsInputs.EncryptedDocument,
                UsesFonts = true,
            },
            [markdown, text, template, title],
            (parse, standard) =>
            {
                if (parse.GetValue(markdown) is not null && parse.GetValue(text) is not null)
                {
                    throw CliErrors.Usage(["Choose at most one of --from-markdown or --from-text; without either the document is blank or the template's."]);
                }

                string? markdownPath = standard.InputFile(markdown);
                string? textPath = standard.InputFile(text);
                string? templatePath = standard.InputFile(template);
                ResolvedOutput output = standard.Output;
                Secret? encryptPassword = standard.EncryptPassword();
                return new WordsCreateRequest
                {
                    Output = output,
                    MarkdownPath = markdownPath,
                    TextPath = textPath,
                    TemplatePath = templatePath,
                    Title = parse.GetValue(title),
                    EncryptPassword = encryptPassword,
                };
            },
            Table)
        {
            Examples =
            [
                "words create report.docx --from-markdown report.md --template brand.docx --title \"Quarterly report\"",
                "words create letter.docx --template letter.dotx",
                "words create notes.docx",
            ],
            Links =
            [
                CommandHelpLink.Docs(WordsModule.Manifest, "report-from-markdown", "a report from Markdown and a template"),
                CommandHelpLink.Docs(WordsModule.Manifest, "document-standards", "professional document construction"),
            ],
        };
    }

    internal static void Table(WordsCreateResult result, TableSurface surface) => ResultText.Produced(surface, result.Output);
}
