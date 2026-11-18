using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// <c>cells inspect</c> — the first step of the projection ladder:
/// structure and metadata, never bulk data.
/// </summary>
internal static class InspectCommand
{
    private const int MaxSampleRows = 100;

    public static CommandDefinition<CellsInspectRequest, CellsInspectResult> Create()
    {
        var sample = new SampleOption($"display values of the first --sample-rows rows and up to {CellsSheetInfo.PreviewColumns} columns of each sheet, from A1");
        var sampleRows = new Option<int>("--sample-rows")
        {
            Description = "Number of sample rows per sheet. Only with --sample.",
            DefaultValueFactory = _ => 5,
        }.InRange(1, MaxSampleRows);
        var detail = new DetailOption(CellsInspectDetails.All, new Dictionary<string, string>
        {
            [CellsInspectDetails.Names] = "defined names",
            [CellsInspectDetails.Errors] = "formula-error scan",
            [CellsInspectDetails.ConditionalFormats] = "conditional-formatting rules",
            [CellsInspectDetails.Layout] = "view, tab color, frozen panes, sizes, outline groups, filter, protection, print area and page setup of each sheet",
        });
        return new(
            "inspect",
            "Show structure and metadata of a workbook.",
            new CommandTraits { Input = CellsInputs.Workbook("Workbook to inspect (xlsx, xlsm, xlsb, xls, ods, csv, ...).") },
            [.. sample.Options, sampleRows, .. detail.Options],
            (parse, standard) =>
            {
                int rows = parse.GetInRange(sampleRows);
                if (!sample.Read(parse) && parse.GetResult(sampleRows) is { Implicit: false })
                {
                    throw CliErrors.OptionInvalid(
                        sampleRows.Name,
                        "--sample-rows sizes the sample, which only --sample includes");
                }
                return new CellsInspectRequest
                {
                    Input = standard.Input,
                    IncludePreview = sample.Read(parse),
                    SampleRows = rows,
                    Details = detail.Read(parse),
                    Password = standard.InputPassword,
                };
            })
        {
            Examples =
            [
                "cells inspect book.xlsx --output-format json",
                "cells inspect book.xlsx --detail charts --detail names --sample",
            ],
            Links =
            [
                CommandHelpLink.Docs(CellsModule.Manifest, "overview", "what each Cells command is for"),
                new("aspose-cli docs reading", "reading every product within bounded windows"),
            ],
        };
    }
}
