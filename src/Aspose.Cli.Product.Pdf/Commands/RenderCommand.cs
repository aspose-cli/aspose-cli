using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class RenderCommand
{
    public static CommandDefinition<PdfRenderRequest, PdfRenderResult> Create()
    {
        var pages = new PartSelectionOptions("page");
        var dpi = new DpiOption();
        return new(
            "render",
            "Render one or more PDF pages.",
            new CommandTraits
            {
                Input = PdfInputs.Document,
                Output = OutputTarget.File("Output path. Default: the input path with the image extension. "
                    + "Several pages write <stem>.<page>.<ext> beside it."),
                UsesFonts = true,
                Target = TargetFormat.Render(PdfFormats.Definitions),
            },
            [.. pages.Options, .. dpi.Options],
            (parse, standard) =>
            {
                PageRange? selection = pages.Read(parse);
                int resolution = dpi.Read(parse, standard.Output.Format);
                ResolvedOutput output = standard.Output;
                return new PdfRenderRequest
                {
                    Input = standard.Input,
                    Output = output,
                    Pages = selection,
                    Dpi = resolution,
                    Password = standard.InputPassword,
                };
            })
        {
            Examples =
            [
                "pdf render report.pdf --pages 1-2 --out page.png",
                "pdf render report.pdf --to svg --pages 3 --out page.svg",
            ],
            Links =
            [
                CommandHelpLink.Docs(PdfModule.Manifest, "overview", "what each PDF command is for"),
            ],
        };
    }
}
