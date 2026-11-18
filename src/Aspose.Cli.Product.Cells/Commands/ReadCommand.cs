using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// <c>cells read</c> — step two of the projection ladder: windowed cell data of one sheet.
/// Reads are budgeted (<c>--max-cells</c>) so output stays affordable for agents; a read over
/// the budget returns its first window and, in window.next, the option values of the next page.
/// </summary>
internal static class ReadCommand
{
    private const int MinMaxCells = 1;
    private const int MaxMaxCells = 1_000_000;

    public static CommandDefinition<CellsReadRequest, CellsReadResult> Create()
    {
        var sheet = new Option<string?>("--sheet")
        {
            Description = "Sheet to read. Default: the active sheet.",
        }.WithInput(InputKind.None);
        var rangeOption = new Option<string?>("--range")
        {
            Description = "Window to read, e.g. A1:F50 or Sales!A1:F50, or all. Default: the used range, subject to --max-cells.",
        }.WithInput(InputKind.None);

        // Set only from window.next: it names the region a planned scan
        // covers, so each page's --range is one window of that region and the chain keeps
        // covering columns the budget could not fit in one window. Hidden — it is
        // CLI-internal plumbing, not a knob a human sets.
        var scanOption = new Option<string?>(ScanContinuation.ScanOption) { Hidden = true }.WithInput(InputKind.None);
        var contentOption = new Option<string>("--content")
        {
            Description = "What each cell carries besides its value and type: its formula f, its styleId and the style pool, or both.",
            DefaultValueFactory = _ => CellsReadContents.Values,
        }.WithInput(InputKind.None);
        contentOption.AcceptOnlyFromAmong(CellsReadContents.Values, CellsReadContents.Formulas, CellsReadContents.Styles, CellsReadContents.Full);
        var maxCellsOption = new Option<int>("--max-cells")
        {
            Description = "Cell budget per call.",
            DefaultValueFactory = _ => 10_000,
        }.InRange(MinMaxCells, MaxMaxCells);
        return new(
            "read",
            "Read cell data of one sheet as a windowed projection.",
            new CommandTraits { Input = CellsInputs.Workbook("Workbook to read.") },
            [sheet, rangeOption, scanOption, contentOption, maxCellsOption],
            (parse, standard) =>
            {
                int maxCells = parse.GetInRange(maxCellsOption);
                string content = parse.GetValue(contentOption) ?? CellsReadContents.Values;
                (string? sheetName, RangeRef? range) = SheetRangeInput.Resolve(
                    parse.GetValue(sheet), parse.GetValue(rangeOption));
                RangeRef? scan = SheetRangeInput.Parse(parse.GetValue(scanOption), ScanContinuation.ScanOption)?.Range;
                return new CellsReadRequest
                {
                    Input = standard.Input,
                    SheetName = sheetName,
                    Range = range,
                    Scan = scan,
                    Content = content,
                    MaxCells = maxCells,
                    Password = standard.InputPassword,
                };
            })
        {
            // A read within the budget is complete. A read over it is a planned scan with
            // another page: of the used range by default, of the data inside an explicit
            // range, or of the region a generated page's command carries.
            Finish = static (_, request, result, _) => result with
            {
                Window = result.Window with { Next = ScanContinuation.Build(result, request.Range, request.Scan, request.MaxCells) },
            },
            Examples =
            [
                "cells read book.xlsx --range Sales!A1:D10 --content values --output-format json",
                "cells read book.xlsx --sheet Sales --content formulas --output-format json",
            ],
            Links =
            [
                CommandHelpLink.Docs(CellsModule.Manifest, "overview", "what each Cells command is for"),
                new("aspose-cli docs reading", "reading every product within bounded windows"),
            ],
        };
    }
}
