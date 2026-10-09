using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Extensibility.Output;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class NewCommand
{
    public static CommandDefinition<NewPresentationRequest, SlidesCreateResult> Create()
    {
        var markdown = new Option<string?>("--from-markdown", "--markdown") { Description = "Markdown outline to author." }.WithInput(InputKind.File);
        var template = new Option<string?>("--template") { Description = "Presentation whose masters, layouts and theme are reused." }.WithInput(InputKind.File);
        var size = new Option<string?>("--size") { Description = "16x9 or 4x3; template size is preserved when omitted." }.WithInput(InputKind.None);
        size.AcceptOnlyFromAmong("16x9", "4x3");
        return new(
            "create",
            "Create a blank, template-based or Markdown-authored presentation.",
            new CommandTraits
            {
                Output = OutputTarget.CreatedFile("PPTX or PPTM path to create.", SlidesFormats.Writable),
                Encrypt = SlidesInputs.EncryptedPresentation,
                UsesFonts = true,
            },
            [markdown, template, size],
            (parse, standard) =>
            {
                string? markdownPath = standard.InputFile(markdown);
                string? templatePath = standard.InputFile(template);
                ResolvedOutput output = standard.Output;
                Secret? encryptPassword = standard.EncryptPassword();
                return new NewPresentationRequest
                {
                    Output = output,
                    MarkdownPath = markdownPath,
                    TemplatePath = templatePath,
                    Size = parse.GetValue(size),
                    EncryptPassword = encryptPassword,
                };
            },
            Table);
    }

    internal static void Table(SlidesCreateResult result, TableSurface surface) =>
        surface.Out.WriteLine(
            $"created {result.Output.Path} ({result.SlideCount} slide(s), {TableText.Bytes(result.Output.SizeBytes)})");
}
