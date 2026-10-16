using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Extensibility.Output;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class CreateCommand
{
    public static CommandDefinition<SlidesCreateRequest, SlidesCreateResult> Create()
    {
        var markdown = new Option<string?>("--from-markdown") { Description = "Markdown outline to author." }.WithInput(InputKind.File);
        var template = new Option<string?>("--template") { Description = "Presentation whose masters, layouts and theme are reused." }.WithInput(InputKind.File);
        var slideSize = new Option<string?>("--slide-size") { Description = "Slide aspect ratio; the template size is kept when omitted." }.WithInput(InputKind.None);
        slideSize.AcceptOnlyFromAmong([.. SlidesSlideSizePresets.All]);
        return new(
            "create",
            "Create a blank, template-based or Markdown-authored presentation.",
            new CommandTraits
            {
                Output = OutputTarget.CreatedFile("PPTX or PPTM path to create.", SlidesFormats.Writable),
                Encrypt = SlidesInputs.EncryptedPresentation,
                UsesFonts = true,
            },
            [markdown, template, slideSize],
            (parse, standard) =>
            {
                SourceFile? markdownFile = standard.Source(markdown);
                SourceFile? templateFile = standard.Source(template);
                ResolvedOutput output = standard.Output;
                Secret? encryptPassword = standard.EncryptPassword();
                return new SlidesCreateRequest
                {
                    Output = output,
                    Markdown = markdownFile,
                    Template = templateFile,
                    SlideSize = parse.GetValue(slideSize),
                    EncryptPassword = encryptPassword,
                };
            },
            Table)
        {
            Examples =
            [
                "slides create deck.pptx --from-markdown outline.md --template brand.pptx",
                "slides create blank.pptx --slide-size 4x3",
            ],
            Links =
            [
                CommandHelpLink.Docs(SlidesModule.Manifest, "deck-from-outline", "a deck from a Markdown outline"),
                CommandHelpLink.Docs(SlidesModule.Manifest, "outline-authoring", "the outline grammar"),
            ],
        };
    }

    internal static void Table(SlidesCreateResult result, TableSurface surface) =>
        ResultText.Produced(surface, result.Output, $"{result.SlideCount} slide(s)");
}
