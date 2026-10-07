using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// <c>cells render</c> — draw a sheet (or a range of it) as an image.
/// This is how an agent "looks at" a spreadsheet to verify layout and charts.
/// </summary>
internal static class RenderCommand
{
    public static Command Create(IProductCommandHost<ICellsEngine> host)
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
        return StandardCommand.Create(
            host,
            "render",
            "Render a sheet, a range, or every visible sheet to images.",
            new CommandTraits
            {
                Input = CellsCommands.Workbook("Workbook to render."),
                Output = OutputTarget.File("Output path. Default: the input path with the image extension. "
                    + "With --all-sheets it is the naming template: <base>.<Sheet><ext>."),
                UsesFonts = true,
                Target = TargetFormat.Render(
                    $"Image format: {string.Join(", ", CellsFormats.Definitions.IdsFor(FormatUse.Render))}.",
                    CellsFormats.Definitions),
            },
            [sheet, rangeOption, allSheetsOption, .. dpi.Options],
            (parse, standard) =>
            {
                string format = standard.TargetFormat();
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
                string output = standard.OutputPath(CellsFormats.Definitions.ExtensionFor(format));
                return standard.OpenEngine().Render(standard.Input, new RenderRequest
                {
                    TargetFormatId = format,
                    OutputPath = output,
                    Overwrite = standard.Overwrite,
                    SheetName = sheetName,
                    Range = range,
                    AllSheets = allSheets,
                    Dpi = resolution,
                    Password = standard.InputPassword,
                });
            }).WithExamples(
            [
                "cells render book.xlsx --range Sales!A1:G20 --out sales.png",
                "cells render book.xlsx --sheet Dashboard --out dashboard.png",
                "cells render book.xlsx --all-sheets --out check.png",
            ]);
    }
}
