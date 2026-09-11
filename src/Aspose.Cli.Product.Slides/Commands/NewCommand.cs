using System.CommandLine;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class NewCommand
{
    public static Command Create(IProductCommandHost<IPresentationEngine> host)
    {
        var file = new Argument<string>("file") { Description = "PPTX or PPTM path to create." }.WithInput(InputKind.None);
        var markdown = new Option<string?>("--from-markdown", "--markdown") { Description = "Markdown outline to author." }.WithInput(InputKind.File);
        var template = new Option<string?>("--template") { Description = "Presentation whose masters, layouts and theme are reused." }.WithInput(InputKind.File);
        var size = new Option<string?>("--size") { Description = "16x9 or 4x3; template size is preserved when omitted." }.WithInput(InputKind.None);
        size.AcceptOnlyFromAmong("16x9", "4x3");
        Option<bool> overwrite = OutputOptions.Overwrite();
        var encrypt = new PasswordOptions("--encrypt", "the output presentation", allowStdin: false);
        var command = new Command("create", "Create a blank, template-based or Markdown-authored presentation.");
        command.Arguments.Add(file);
        command.Options.Add(markdown);
        command.Options.Add(template);
        command.Options.Add(size);
        command.Options.Add(overwrite);
        encrypt.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            string? markdownValue = parse.GetValue(markdown);
            string? templateValue = parse.GetValue(template);
            return context.Port.Create(new NewPresentationRequest
            {
                OutputPath = context.Paths.ResolveOutput(parse.GetRequiredValue(file)),
                Overwrite = parse.GetValue(overwrite),
                MarkdownPath = markdownValue is null ? null : context.Paths.ResolveInput(markdownValue),
                TemplatePath = templateValue is null ? null : context.Paths.ResolveInput(templateValue),
                Size = parse.GetValue(size),
                EncryptPassword = encrypt.Resolve(parse, context.Inputs),
            });
        }));
        return command;
    }
}
