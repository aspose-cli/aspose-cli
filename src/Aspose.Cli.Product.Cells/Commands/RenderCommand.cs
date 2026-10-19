using System.CommandLine;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Output;
using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// <c>cells render</c> — draw sheets (or a range of one) as images.
/// This is how an agent "looks at" a spreadsheet to verify layout and charts.
/// </summary>
internal static class RenderCommand
{
    public static CommandDefinition<CellsRenderRequest, CellsRenderResult> Create()
    {
        var sheets = new PartRangeOption("sheet", $"every visible sheet, at most {RenderSelection.MaximumParts} per run")
        {
        };
        var rangeOption = new Option<string?>(SheetRangeInput.RangeOption)
        {
            Description = "Render only this range of one sheet, e.g. A1:G20 or Sales!A1:G20, or all.",
        }.WithInput(InputKind.None);
        var dpi = new DpiOption();
        return new(
            "render",
            "Render sheets, or a range of one, to images.",
            new CommandTraits
            {
                Input = CellsInputs.Workbook("Workbook to render."),
                Output = OutputTarget.File("Output path. Default: the input path with the image extension. "
                    + "Several sheets write <stem>.<sheet>.<ext> beside it."),
                UsesFonts = true,
                Target = TargetFormat.Render(CellsFormats.Definitions),
            },
            [.. sheets.Options, rangeOption, .. dpi.Options],
            (parse, standard) =>
            {
                ResolvedOutput output = standard.Output;
                int resolution = dpi.Read(parse, output.Format);
                (PageRange? selection, string? text) = CellsInputs.SheetSelection(sheets, parse);
                RangeSpec? range = SheetRangeInput.Parse(parse.GetValue(rangeOption));
                if (range?.SheetName is not null && selection is not null)
                {
                    throw CliErrors.OptionInvalid(
                        sheets.Name,
                        "the range already names its sheet",
                        "Qualify the sheet in one place only: drop --sheets, or the sheet name of --range.");
                }

                return new CellsRenderRequest
                {
                    Input = standard.Input,
                    Output = output,
                    Sheets = selection,
                    SheetsText = text,
                    SheetName = range?.SheetName,
                    Range = range?.Range,
                    Dpi = resolution,
                    Password = standard.InputPassword,
                };
            },
            Table)
        {
            Examples =
            [
                "cells render book.xlsx --range Sales!A1:G20 --out sales.png",
                "cells render book.xlsx --sheets 2 --out dashboard.png",
                "cells render book.xlsx --sheets all --out check.png",
            ],
            Links =
            [
                CommandHelpLink.Docs(CellsModule.Manifest, "verification", "the spreadsheet delivery verification protocol"),
                CommandHelpLink.Docs(CellsModule.Manifest, "preview", "previewing a workbook while it changes"),
            ],
        };
    }

    internal static void Table(CellsRenderResult render, TableSurface surface)
    {
        var notes = new List<string>(2);
        if (render.Range is { } range)
        {
            notes.Add($"range {range}");
        }

        if (render.Dpi is { } dpi)
        {
            notes.Add(TableText.Int(dpi) + " dpi");
        }

        string? note = notes.Count == 0 ? null : string.Join(", ", notes);
        ResultText.Files(surface, "rendered", "sheet", [.. render.Outputs.Select(output =>
            new ProducedFile(output.Sheet, output.Path, output.Format, output.SizeBytes, note))]);
    }
}
