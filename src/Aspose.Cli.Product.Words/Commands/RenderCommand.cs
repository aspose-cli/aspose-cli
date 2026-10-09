using Aspose.Cli.Sdk.Extensibility.Output;

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
                Output = OutputTarget.File("Output path; multi-page output adds .pN before the extension."),
                UsesFonts = true,
                Target = TargetFormat.Render(WordsFormats.Definitions),
            },
            [.. pages.Options, .. dpi.Options],
            (parse, standard) =>
            {
                PartSelection selection = pages.Read(parse);
                int resolution = dpi.Read(parse);
                return new WordsRenderRequest
                {
                    Input = standard.Input,
                    Output = standard.Output,
                    Pages = selection.Range,
                    AllPages = selection.All,
                    Dpi = resolution,
                    Password = standard.InputPassword,
                };
            },
            Render)
        {
            Examples =
            [
                "words render contract.docx --pages 1-2 --out review.png",
                "words render contract.docx --all-pages --dpi 192 --out review.png",
            ],
        };
    }

    internal static void Render(WordsRenderResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"rendered {result.Outputs.Count} page(s)");
        foreach (PageOutput page in result.Outputs)
        {
            surface.Out.WriteLine($"  page {page.Page}: {page.Output.Path} ({TableText.Bytes(page.Output.SizeBytes)})");
        }
    }
}
