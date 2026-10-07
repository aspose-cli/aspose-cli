using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Words.Commands;

internal static class NewCommand
{
    public static Command Create(IProductCommandHost<IWordsEngine> host)
    {
        var blank = new Option<bool>("--blank") { Description = "Create a blank document." };
        var markdown = new Option<string?>("--markdown") { Description = "Create from a Markdown file." }.WithInput(InputKind.File);
        var text = new Option<string?>("--text") { Description = "Create from a UTF-8 text file." }.WithInput(InputKind.File);
        var template = new Option<string?>("--template") { Description = "Document whose styles, page setup, headers and footers the new document uses; its body is replaced by --markdown or --text content." }.WithInput(InputKind.File);
        var title = new Option<string?>("--title") { Description = "Set the built-in title property." }.WithInput(InputKind.None);
        return StandardCommand.Create(
            host,
            "create",
            "Create a document from one content source, optionally inside a template.",
            new CommandTraits
            {
                Output = OutputTarget.CreatedFile("Document path to create.", WordsFormats.Writable),
                Encrypt = WordsCommands.EncryptedDocument,
                UsesFonts = true,
            },
            [blank, markdown, text, template, title],
            (parse, standard) =>
            {
                string? markdownValue = parse.GetValue(markdown);
                string? textValue = parse.GetValue(text);
                string? templateValue = parse.GetValue(template);
                bool isBlank = parse.GetValue(blank);
                int contents = (isBlank ? 1 : 0) + (markdownValue is null ? 0 : 1) + (textValue is null ? 0 : 1);
                if (contents + (templateValue is null ? 0 : 1) == 0
                    || contents > 1
                    || (isBlank && templateValue is not null))
                {
                    throw CliErrors.Usage(["Choose --blank, --template, or one of --markdown or --text with an optional --template."]);
                }

                string? markdownPath = standard.InputFile(markdown);
                string? textPath = standard.InputFile(text);
                string? templatePath = standard.InputFile(template);
                ResolvedOutput output = standard.Output;
                string? encryptPassword = standard.EncryptPassword();
                return standard.OpenEngine().Create(new NewDocumentRequest
                {
                    Output = output,
                    MarkdownPath = markdownPath,
                    TextPath = textPath,
                    TemplatePath = templatePath,
                    Title = parse.GetValue(title),
                    EncryptPassword = encryptPassword,
                });
            })
            .WithExamples(
            [
                "words create report.docx --markdown report.md --template brand.docx --title \"Quarterly report\"",
                "words create letter.docx --template letter.dotx",
            ]);
    }
}
