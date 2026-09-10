using System.CommandLine;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class NewCommand
{
    public static Command Create(IProductCommandHost<IDocumentEngine> host)
    {
        var output = new Argument<string>("file") { Description = "Document path to create." };
        var blank = new Option<bool>("--blank") { Description = "Create a blank document." };
        var markdown = new Option<string?>("--markdown") { Description = "Create from a Markdown file." };
        var text = new Option<string?>("--text") { Description = "Create from a UTF-8 text file." };
        var template = new Option<string?>("--template") { Description = "Create from a document template." };
        var title = new Option<string?>("--title") { Description = "Set the built-in title property." };
        Option<bool> overwrite = OutputOptions.Overwrite();
        var encrypt = new PasswordOptions("--encrypt", "the output document", allowStdin: false);

        var command = new Command("create", "Create a document from exactly one source.");
        command.Arguments.Add(output);
        command.Options.Add(blank);
        command.Options.Add(markdown);
        command.Options.Add(text);
        command.Options.Add(template);
        command.Options.Add(title);
        command.Options.Add(overwrite);
        encrypt.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            string? markdownValue = parse.GetValue(markdown);
            string? textValue = parse.GetValue(text);
            string? templateValue = parse.GetValue(template);
            int sources = (parse.GetValue(blank) ? 1 : 0) + (markdownValue is null ? 0 : 1)
                + (textValue is null ? 0 : 1) + (templateValue is null ? 0 : 1);
            if (sources != 1)
            {
                throw CliErrors.Usage(["Choose exactly one of --blank, --markdown, --text or --template."]);
            }

            return context.Port.CreateDocument(new NewDocumentRequest
            {
                OutputPath = context.Paths.ResolveOutput(parse.GetRequiredValue(output)),
                Overwrite = parse.GetValue(overwrite),
                MarkdownPath = markdownValue is null ? null : context.Paths.ResolveInput(markdownValue),
                TextPath = textValue is null ? null : context.Paths.ResolveInput(textValue),
                TemplatePath = templateValue is null ? null : context.Paths.ResolveInput(templateValue),
                Title = parse.GetValue(title),
                EncryptPassword = encrypt.Resolve(parse, context.Inputs),
            });
        }));
        return command;
    }
}
