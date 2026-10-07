using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// <c>cells inspect</c> — the first step of the projection ladder:
/// structure and metadata, never bulk data.
/// </summary>
internal static class InfoCommand
{
    private const int MaxPreviewRows = 100;

    public static Command Create(IProductCommandHost<ICellsEngine> host)
    {
        var preview = new Option<bool>("--preview")
        {
            Description = "Include a small sample of display values for each sheet.",
        };
        var previewRows = new Option<int>("--preview-rows")
        {
            Description = $"Number of preview rows per sheet (1-{MaxPreviewRows}).",
            DefaultValueFactory = _ => 5,
        };
        var detail = new Option<string[]>("--detail")
        {
            Description = "Extra sections: names (defined names), errors (formula-error scan), "
                + "fonts (fonts used), tables, charts, pivots, validation, layout (frozen panes, outline groups, filter, "
                + "print area and page setup of each sheet). Repeatable.",
            AllowMultipleArgumentsPerToken = true,
        }.WithInput(InputKind.None);
        detail.AcceptOnlyFromAmong([.. InfoDetails.All]);
        return StandardCommand.Create(
            host,
            "inspect",
            "Show structure and metadata of a workbook.",
            new CommandTraits { Input = CellsCommands.Workbook("Workbook to inspect (xlsx, xlsm, xlsb, xls, ods, csv, ...).") },
            [preview, previewRows, detail],
            (parse, standard) =>
            {
                int rows = parse.GetValue(previewRows);
                OptionGuards.EnsureInRange("--preview-rows", rows, 1, MaxPreviewRows,
                    "Pass a smaller sample size; previews are meant to be cheap to read.");
                return standard.OpenEngine().GetInfo(standard.Input, new InfoRequest
                {
                    IncludePreview = parse.GetValue(preview),
                    PreviewRows = rows,
                    Details = parse.GetValue(detail),
                    Password = standard.InputPassword,
                });
            }).WithExamples(
            [
                "cells inspect book.xlsx --output json",
                "cells inspect book.xlsx --detail charts names --preview",
            ]);
    }
}
