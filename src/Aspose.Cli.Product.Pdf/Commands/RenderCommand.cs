using System.CommandLine;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class RenderCommand
{
    public static Command Create(IProductCommandHost<IPdfEngine> host)
    {
        var pages = new PartSelectionOptions("page");
        var dpi = new DpiOption();
        return StandardCommand.Create(
            host,
            "render",
            "Render one or more PDF pages.",
            new CommandTraits
            {
                Input = PdfCommands.Document,
                Output = OutputTarget.File("Output path; multi-page output adds .pN before the extension."),
                UsesFonts = true,
                Target = TargetFormat.Render("png, jpeg or svg.", PdfFormats.Definitions),
            },
            [.. pages.Options, .. dpi.Options],
            (parse, standard) =>
            {
                PartSelection selection = pages.Read(parse);
                int resolution = dpi.Read(parse);
                string format = standard.TargetFormat();
                return standard.OpenEngine().Render(standard.Input, new PdfRenderRequest
                {
                    TargetFormatId = format,
                    OutputPath = standard.OutputPath(PdfFormats.Definitions.ExtensionFor(format)),
                    Overwrite = standard.Overwrite,
                    Pages = selection.Range,
                    AllPages = selection.All,
                    Dpi = resolution,
                    Password = standard.InputPassword,
                });
            });
    }
}
