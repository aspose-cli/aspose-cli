using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class RenderCommand
{
    public static Command Create(IProductCommandHost<IPdfEngine> host)
    {
        var pages = new PartSelectionOptions("page");
        var dpi = new DpiOption();
        var grid = new Option<int?>("--grid")
        {
            Description = "Draw a labelled coordinate grid with a line every <grid> points (10-500) on png or jpeg output, "
                + "measured from the top-left corner as redact_area takes them.",
        };
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
            [.. pages.Options, .. dpi.Options, grid],
            (parse, standard) =>
            {
                PartSelection selection = pages.Read(parse);
                int resolution = dpi.Read(parse);
                ResolvedOutput output = standard.Output;
                return standard.OpenEngine().Render(standard.Input, new PdfRenderRequest
                {
                    Output = output,
                    Pages = selection.Range,
                    AllPages = selection.All,
                    Dpi = resolution,
                    Grid = parse.GetValue(grid),
                    Password = standard.InputPassword,
                });
            });
    }
}
