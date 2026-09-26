using System.CommandLine;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class RenderCommand
{
    public static Command Create(IProductCommandHost<IWordsEngine> host)
    {
        var pages = new PartSelectionOptions("page");
        var dpi = new DpiOption();
        return StandardCommand.Create(
            host,
            "render",
            "Render one or more document pages.",
            new CommandTraits
            {
                Input = WordsCommands.Document,
                Output = OutputTarget.File("Output path; multi-page output adds .pN before the extension."),
                UsesFonts = true,
                Target = TargetFormat.Render("png, jpeg or svg.", WordsFormats.Definitions),
            },
            [.. pages.Options, .. dpi.Options],
            (parse, standard) =>
            {
                PartSelection selection = pages.Read(parse);
                int resolution = dpi.Read(parse);
                string format = standard.TargetFormat();
                return standard.OpenEngine().Render(standard.Input, new WordsRenderRequest
                {
                    TargetFormatId = format,
                    OutputPath = standard.OutputPath(WordsFormats.Definitions.ExtensionFor(format)),
                    Overwrite = standard.Overwrite,
                    Pages = selection.Range,
                    AllPages = selection.All,
                    Dpi = resolution,
                    Password = standard.InputPassword,
                });
            })
            .WithExamples(
            [
                "words render contract.docx --pages 1-2 --out review.png",
                "words render contract.docx --all-pages --dpi 192 --out review.png",
            ]);
    }
}
