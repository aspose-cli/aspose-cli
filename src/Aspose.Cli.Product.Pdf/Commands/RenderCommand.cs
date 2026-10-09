using System.CommandLine;
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
            Description = "Draw a labelled coordinate grid with a line every <grid> points (10-500) on png or jpeg output, "
                + "measured from the top-left corner as redact_area takes them.",
        };
        return new(
            "render",
            "Render one or more PDF pages.",
            new CommandTraits
            {
                Input = PdfInputs.Document,
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
                return new PdfRenderRequest
                {
                    Input = standard.Input,
                    Output = output,
                    Pages = selection.Range,
                    AllPages = selection.All,
                    Dpi = resolution,
                    Grid = parse.GetValue(grid),
                    Password = standard.InputPassword,
                };
            },
            Table);
    }

    internal static void Table(PdfRenderResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"rendered {result.Outputs.Count} page(s)");
        foreach (PdfPageOutput output in result.Outputs)
        {
            surface.Out.WriteLine(
                $"  page {output.Page}: {output.Output.Path} ({output.Output.Format}, {TableText.Bytes(output.Output.SizeBytes)})");
        }

        if (result.Grid is { } grid)
        {
            surface.Out.WriteLine(
                $"grid: lines every {grid.Spacing} {grid.Unit}, labelled every {grid.LabelSpacing} {grid.Unit}, origin {grid.Origin}");
        }
    }
}
