using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility.Output;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// <c>cells render</c> — draw a sheet (or a range of it) as an image.
/// This is how an agent "looks at" a spreadsheet to verify layout and charts.
/// </summary>
internal static class RenderCommand
{
    public static CommandDefinition<RenderRequest, RenderResult> Create()
    {
        var sheet = new Option<string?>("--sheet")
        {
            Description = "Sheet to render. Default: the active sheet.",
        }.WithInput(InputKind.None);
        var rangeOption = new Option<string?>("--range")
        {
            Description = "Render only this range, e.g. A1:G20 or Sales!A1:G20.",
        }.WithInput(InputKind.None);
        var allSheetsOption = new Option<bool>("--all-sheets")
        {
            Description = "Render every visible sheet, one image per sheet named <out-base>.<Sheet><ext>.",
        };
        var dpi = new DpiOption();
        return new(
            "render",
            "Render a sheet, a range, or every visible sheet to images.",
            new CommandTraits
            {
                Input = CellsTraits.Workbook("Workbook to render."),
                Output = OutputTarget.File("Output path. Default: the input path with the image extension. "
                    + "With --all-sheets it is the naming template: <base>.<Sheet><ext>."),
                UsesFonts = true,
                Target = TargetFormat.Render(CellsFormats.Definitions),
            },
            [sheet, rangeOption, allSheetsOption, .. dpi.Options],
            (parse, standard) =>
            {
                ResolvedOutput output = standard.Output;
                int resolution = dpi.Read(parse);
                bool allSheets = parse.GetValue(allSheetsOption);
                if (allSheets && parse.GetValue(sheet) is not null)
                {
                    throw CliErrors.OptionInvalid(
                        "--all-sheets",
                        "cannot be combined with --sheet",
                        "Choose one: render every visible sheet (--all-sheets) or one named sheet (--sheet).");
                }

                if (allSheets && parse.GetValue(rangeOption) is not null)
                {
                    throw CliErrors.OptionInvalid(
                        "--all-sheets",
                        "cannot be combined with --range",
                        "Choose one: render every visible sheet (--all-sheets) or a window of one sheet (--range).");
                }

                (string? sheetName, RangeRef? range) = SheetRangeInput.Resolve(
                    parse.GetValue(sheet), parse.GetValue(rangeOption));
                return new RenderRequest
                {
                    Input = standard.Input,
                    Output = output,
                    SheetName = sheetName,
                    Range = range,
                    AllSheets = allSheets,
                    Dpi = resolution,
                    Password = standard.InputPassword,
                };
            },
            Table)
        {
            Examples =
            [
                "cells render book.xlsx --range Sales!A1:G20 --out sales.png",
                "cells render book.xlsx --sheet Dashboard --out dashboard.png",
                "cells render book.xlsx --all-sheets --out check.png",
            ],
        };
    }

    internal static void Table(RenderResult render, TableSurface surface)
    {
        if (render.Outputs is { } outputs)
        {
            // The --all-sheets shape: the per-sheet list IS the result, so a
            // human sees every produced file, not just the first-sheet summary.
            string dpi = render.Dpi is { } value ? $", {TableText.Int(value)} dpi" : string.Empty;
            surface.Out.WriteLine($"rendered {TableText.Int(outputs.Count)} sheet(s) ({render.Output.Format}{dpi})");
            foreach (SheetRenderOutput output in outputs)
            {
                surface.Out.WriteLine($"  {output.Sheet}: {output.Path} ({TableText.Bytes(output.SizeBytes)})");
            }

            return;
        }

        ResultText.Produced(surface, render.Output, Describe(render));
    }

    private static string Describe(RenderResult render)
    {
        var parts = new List<string>(3) { $"sheet {render.Sheet}" };
        if (render.Range is { } range)
        {
            parts.Add($"range {range}");
        }

        if (render.Dpi is { } dpi)
        {
            parts.Add(TableText.Int(dpi) + " dpi");
        }

        return string.Join(", ", parts);
    }
}
