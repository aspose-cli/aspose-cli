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
                Output = OutputTarget.File("Output path; several pages write <stem>.<page>.<ext> beside it."),
                UsesFonts = true,
                Target = TargetFormat.Render(WordsFormats.Definitions),
            },
            [.. pages.Options, .. dpi.Options],
            (parse, standard) =>
            {
                PageRange? selection = pages.Read(parse);
                int resolution = dpi.Read(parse);
                return new WordsRenderRequest
                {
                    Input = standard.Input,
                    Output = standard.Output,
                    Pages = selection,
                    Dpi = resolution,
                    Password = standard.InputPassword,
                };
            },
            Table)
        {
            Examples =
            [
                "words render contract.docx --pages 1-2 --out review.png",
                "words render contract.docx --dpi 192 --out review.png",
            ],
        };
    }

    internal static void Table(WordsRenderResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"rendered {result.Outputs.Count} page(s)");
        foreach (PageOutput page in result.Outputs)
        {
            surface.Out.WriteLine($"  page {page.Page}: {page.Output.Path} ({TableText.Bytes(page.Output.SizeBytes)})");
        }
    }
}
