using System.CommandLine;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class NewCommand
{
    public static Command Create(IProductCommandHost<ISlidesEngine> host)
    {
        var markdown = new Option<string?>("--from-markdown", "--markdown") { Description = "Markdown outline to author." }.WithInput(InputKind.File);
        var template = new Option<string?>("--template") { Description = "Presentation whose masters, layouts and theme are reused." }.WithInput(InputKind.File);
        var size = new Option<string?>("--size") { Description = "16x9 or 4x3; template size is preserved when omitted." }.WithInput(InputKind.None);
        size.AcceptOnlyFromAmong("16x9", "4x3");
        return StandardCommand.Create(
            host,
            "create",
            "Create a blank, template-based or Markdown-authored presentation.",
            new CommandTraits
            {
                Output = OutputTarget.CreatedFile("PPTX or PPTM path to create."),
                Encrypt = SlidesCommands.EncryptedPresentation,
                UsesFonts = true,
            },
            [markdown, template, size],
            (parse, standard) =>
            {
                string? markdownPath = standard.InputFile(markdown);
                string? templatePath = standard.InputFile(template);
                string outputPath = standard.CreatedPath;
                string? encryptPassword = standard.EncryptPassword(SlidesFormats.ForOutput(outputPath));
                return standard.OpenEngine().Create(new NewPresentationRequest
                {
                    OutputPath = outputPath,
                    Overwrite = standard.Overwrite,
                    MarkdownPath = markdownPath,
                    TemplatePath = templatePath,
                    Size = parse.GetValue(size),
                    EncryptPassword = encryptPassword,
                });
            });
    }
}
