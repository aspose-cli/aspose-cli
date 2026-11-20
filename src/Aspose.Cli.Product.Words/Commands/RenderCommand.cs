using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class RenderCommand
{
    public static CommandDefinition<WordsRenderRequest, WordsRenderResult> Create()
    {
        var pages = new PartSelectionOptions("page");
        var dpi = new DpiOption();
        return new(
            "render",
            "Render one or more document pages.",
            new CommandTraits
            {
                Input = WordsInputs.Document,
                Output = OutputTarget.File("Output path. Default: the input path with the image extension. "
                    + "Several pages write <stem>.<page>.<ext> beside it."),
                UsesFonts = true,
                Target = TargetFormat.Render(WordsFormats.Definitions),
            },
            [.. pages.Options, .. dpi.Options],
            (parse, standard) =>
            {
                PageRange? selection = pages.Read(parse);
                int resolution = dpi.Read(parse, standard.Output.Format);
                return new WordsRenderRequest
                {
                    Input = standard.Input,
                    Output = standard.Output,
                    Pages = selection,
                    Dpi = resolution,
                    Password = standard.InputPassword,
                };
            })
        {
            Examples =
            [
                "words render contract.docx --pages 1-2 --out review.png",
                "words render contract.docx --dpi 192 --out review.png",
            ],
            Links =
            [
                CommandHelpLink.Schema(WordsModule.Manifest, "render-result", "the fields of the render result"),
            ],
        };
    }
}
