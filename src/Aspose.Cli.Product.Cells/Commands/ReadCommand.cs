using System.CommandLine;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Contracts.Addressing;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// <c>cells query range</c> — step two of the projection ladder: windowed
/// cell data of one sheet. Reads are budgeted (<c>--max-cells</c>) so output
/// stays affordable for agents; a default read over budget degrades to a summary plus a
/// ready-to-run window.next command, and an explicit range over budget is refused with the
/// command that scans it page by page.
/// </summary>
internal static class ReadCommand
{
    private const int MinMaxCells = 1;
    private const int MaxMaxCells = 1_000_000;

    public static Command Create(IProductCommandHost<ICellsEngine> host)
    {
        var sheet = new Option<string?>("--sheet")
        {
            Description = "Sheet to read. Default: the active sheet.",
        }.WithInput(InputKind.None);
        var rangeOption = new Option<string?>("--range")
        {
            Description = "Window to read, e.g. A1:F50 or Sales!A1:F50. Default: the used range, subject to --max-cells.",
        }.WithInput(InputKind.None);

        // Set only by a generated window.next command: it names the region a planned scan
        // covers, so each page's --range is one window of that region and the chain keeps
        // covering columns the budget could not fit in one window. Hidden — it is
        // CLI-internal plumbing, not a knob a human sets.
        var scanOption = new Option<string?>(NextReadCommand.ScanOption) { Hidden = true }.WithInput(InputKind.None);
        var scopeOption = new Option<string>("--scope")
        {
            Description = "Projection scope: values (default), formulas (adds f), styles (adds styleId + pool), full.",
            DefaultValueFactory = _ => ReadScopes.Values,
        }.WithInput(InputKind.None);
        scopeOption.AcceptOnlyFromAmong(ReadScopes.Values, ReadScopes.Formulas, ReadScopes.Styles, ReadScopes.Full);
        var maxCellsOption = new Option<int>("--max-cells")
        {
            Description = $"Cell budget per call ({MinMaxCells}-{MaxMaxCells}).",
            DefaultValueFactory = _ => 10_000,
        };
        return StandardCommand.Create(
            host,
            "range",
            "Read cell data of one sheet as a windowed projection.",
            new CommandTraits { Input = CellsCommands.Workbook("Workbook to read.") },
            [sheet, rangeOption, scanOption, scopeOption, maxCellsOption],
            (parse, standard) =>
            {
                int maxCells = parse.GetValue(maxCellsOption);
                OptionGuards.EnsureInRange("--max-cells", maxCells, MinMaxCells, MaxMaxCells,
                    "Keep the budget modest; page through large sheets with each window.next command instead.");
                _ = ReadScopeExtensions.TryParse(parse.GetValue(scopeOption), out ReadScope scope);
                (string? sheetName, RangeRef? range) = SheetRangeInput.Resolve(
                    parse.GetValue(sheet), parse.GetValue(rangeOption));
                RangeRef? scan = parse.GetValue(scanOption) is { } region ? A1.ParseRange(region).Range : null;

                string input = standard.Input;
                if (range is { } explicitRange && explicitRange.CellCount > maxCells)
                {
                    throw CellsErrors.RangeTooLarge(
                        explicitRange.CellCount,
                        maxCells,
                        "Scan the range in budgeted windows: run "
                            + NextReadCommand.First(standard.Continuation(), sheetName, explicitRange, scope.ToContractName(), maxCells)
                            + " and follow each window.next command, or raise --max-cells.");
                }

                WorkbookReadResult result = standard.OpenEngine().Read(input, new ReadRequest
                {
                    SheetName = sheetName,
                    Range = range,
                    Scan = scan,
                    Scope = scope,
                    MaxCells = maxCells,
                    Password = standard.InputPassword,
                });

                // An explicit range is a complete, bounded request. Only a CLI-planned scan
                // has another page: a default read scans the used range, and a generated
                // page scans the region its command carries.
                return result with
                {
                    Window = result.Window! with
                    {
                        Next = NextReadCommand.Build(standard.Continuation(), result, range, scan, maxCells),
                    },
                };
            }).WithExamples(
            [
                "cells query range book.xlsx --range Sales!A1:D10 --scope values --output json",
                "cells query range book.xlsx --sheet Sales --scope formulas --output json",
            ]);
    }
}
