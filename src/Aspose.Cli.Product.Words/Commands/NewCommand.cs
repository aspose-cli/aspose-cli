using System.CommandLine;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class NewCommand
{
    public static Command Create(IProductCommandHost<IDocumentEngine> host)
    {
        var output = new Argument<string>("file") { Description = "Document path to create." }.WithInput(InputKind.None);
        var blank = new Option<bool>("--blank") { Description = "Create a blank document." };
        var markdown = new Option<string?>("--markdown") { Description = "Create from a Markdown file." }.WithInput(InputKind.File);
        var text = new Option<string?>("--text") { Description = "Create from a UTF-8 text file." }.WithInput(InputKind.File);
        var template = new Option<string?>("--template") { Description = "Document whose styles, page setup, headers and footers the new document uses; its body is replaced by --markdown or --text content." }.WithInput(InputKind.File);
        var title = new Option<string?>("--title") { Description = "Set the built-in title property." }.WithInput(InputKind.None);
        Option<bool> overwrite = OutputOptions.Overwrite();
        var encrypt = new PasswordOptions("--encrypt", "the output document", allowStdin: false);
        var fonts = new FontDirectoryOptions();

        var command = new Command("create", "Create a document from one content source, optionally inside a template.");
        command.Arguments.Add(output);
        command.Options.Add(blank);
        command.Options.Add(markdown);
        command.Options.Add(text);
        command.Options.Add(template);
        command.Options.Add(title);
        command.Options.Add(overwrite);
        encrypt.AddTo(command);
        fonts.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
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

            string? markdownPath = markdownValue is null ? null : context.Paths.ResolveInput(markdownValue);
            string? textPath = textValue is null ? null : context.Paths.ResolveInput(textValue);
            string? templatePath = templateValue is null ? null : context.Paths.ResolveInput(templateValue);
            string outputPath = OutputFileOptions.ResolveExplicit(
                context.Paths, parse.GetRequiredValue(output), output.Name, markdownPath, textPath, templatePath);
            encrypt.EnsureProtectable(parse, WordsFormats.ForOutput(outputPath), WordsFormats.EncryptIds);
            using IDisposable fontScope = fonts.Use(parse, context);
            return context.Port.CreateDocument(new NewDocumentRequest
            {
                OutputPath = outputPath,
                Overwrite = parse.GetValue(overwrite),
                MarkdownPath = markdownPath,
                TextPath = textPath,
                TemplatePath = templatePath,
                Title = parse.GetValue(title),
                EncryptPassword = encrypt.Resolve(parse, context.Inputs, context.ReadEnvironment),
            });
        }));
        return command;
    }
}
