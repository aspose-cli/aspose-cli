using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Output;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class RenderCommand
{
    public static CommandDefinition<PdfRenderRequest, PdfRenderResult> Create()
    {
        var pages = new PartSelectionOptions("page");
        var dpi = new DpiOption();
        var grid = new Option<int?>("--grid")
        {
            Description = "Draw a labelled coordinate grid with a line every <grid> points on png or jpeg output, "
                + "measured from the top-left corner as redact_area takes them.",
        }.InRange(PdfRenderRequest.MinimumGrid, PdfRenderRequest.MaximumGrid);
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
            [.. pages.Options, .. dpi.Options, grid],
            (parse, standard) =>
            {
                PageRange? selection = pages.Read(parse);
                int resolution = dpi.Read(parse);
                ResolvedOutput output = standard.Output;
                return new PdfRenderRequest
                {
                    Input = standard.Input,
                    Output = output,
                    Pages = selection,
                    Dpi = resolution,
                    Grid = parse.GetInRange(grid, "Choose a spacing such as --grid 50."),
                    Password = standard.InputPassword,
                };
            },
            Table)
        {
            Examples =
            [
                "pdf render report.pdf --pages 1-2 --out page.png",
                "pdf render report.pdf --pages 3 --grid 50 --out grid.png",
            ],
            Links =
            [
                CommandHelpLink.Docs(PdfModule.Manifest, "verification", "read-back, rendering and PDF/A evidence"),
                CommandHelpLink.Docs(PdfModule.Manifest, "editing", "the page coordinates redact_area takes"),
            ],
        };
    }

    internal static void Table(PdfRenderResult result, TableSurface surface)
    {
        ResultText.Files(surface, "rendered", "page", [.. result.Outputs.Select(static output =>
            new ProducedFile($"page {output.Page}", output.Path, output.Format, output.SizeBytes))]);

        if (result.Grid is { } grid)
        {
            surface.Out.WriteLine(
                $"grid: lines every {grid.Spacing} {grid.Unit}, labelled every {grid.LabelSpacing} {grid.Unit}, origin {grid.Origin}");
        }
    }
}
