using System.CommandLine;
using System.Globalization;
using Aspose.Cli.Sdk.Extensibility.Output;

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

    public static CommandDefinition<ReadRequest, WorkbookReadResult> Create()
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
        var scanOption = new Option<string?>(ScanContinuation.ScanOption) { Hidden = true }.WithInput(InputKind.None);
        var scopeOption = new Option<string>("--scope")
        {
            Description = "Projection scope: values, formulas (adds f), styles (adds styleId + pool), full.",
            DefaultValueFactory = _ => ReadScopes.Values,
        }.WithInput(InputKind.None);
        scopeOption.AcceptOnlyFromAmong(ReadScopes.Values, ReadScopes.Formulas, ReadScopes.Styles, ReadScopes.Full);
        var maxCellsOption = new Option<int>("--max-cells")
        {
            Description = $"Cell budget per call ({MinMaxCells}-{MaxMaxCells}).",
            DefaultValueFactory = _ => 10_000,
        };
        return new(
            "range",
            "Read cell data of one sheet as a windowed projection.",
            new CommandTraits { Input = CellsInputs.Workbook("Workbook to read.") },
            [sheet, rangeOption, scanOption, scopeOption, maxCellsOption],
            (parse, standard) =>
            {
                int maxCells = parse.GetValue(maxCellsOption);
                OptionGuards.EnsureInRange("--max-cells", maxCells, MinMaxCells, MaxMaxCells,
                    "Keep the budget modest; page through large sheets with each window.next command instead.");
                string scope = parse.GetValue(scopeOption) ?? ReadScopes.Values;
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
                            + ScanContinuation.First(standard.Continuation(), sheetName, explicitRange, scope, maxCells)
                            + " and follow each window.next command, or raise --max-cells.");
                }

                return new ReadRequest
                {
                    Input = input,
                    SheetName = sheetName,
                    Range = range,
                    Scan = scan,
                    Scope = scope,
                    MaxCells = maxCells,
                    Password = standard.InputPassword,
                };
            },
            Table)
        {
            // An explicit range is a complete, bounded request. Only a CLI-planned scan
            // has another page: a default read scans the used range, and a generated
            // page scans the region its command carries.
            Finish = static (_, request, result, standard) => result with
            {
                Window = result.Window with
                {
                    Next = ScanContinuation.Build(standard.Continuation(), result, request.Range, request.Scan, request.MaxCells),
                },
            },
            Examples =
            [
                "cells query range book.xlsx --range Sales!A1:D10 --scope values --output json",
                "cells query range book.xlsx --sheet Sales --scope formulas --output json",
            ],
        };
    }

    internal static void Table(WorkbookReadResult read, TableSurface surface)
    {
        SheetProjection sheet = read.Sheet;
        var headline = new List<string> { sheet.Name };
        if (sheet.Range is { } returned)
        {
            headline.Add(returned);
        }

        if (sheet.UsedRange is { } used)
        {
            headline.Add($"(used {used})");
        }

        surface.Out.WriteLine(string.Join(' ', headline));

        if (sheet.Cells is { Count: > 0 } cells && sheet.Range is not null)
        {
            RangeRef range = A1.ParseRange(sheet.Range).Range;

            string[] headers = new string[range.ColumnCount + 1];
            headers[0] = string.Empty;
            for (int column = 0; column < range.ColumnCount; column++)
            {
                headers[column + 1] = A1.ColumnName(range.Start.Column + column);
            }

            var table = new TextTable(headers);
            for (int row = 0; row < cells.Count; row++)
            {
                string[] line = new string[range.ColumnCount + 1];
                line[0] = TableText.Int(range.Start.Row + row + 1);
                for (int column = 0; column < cells[row].Count; column++)
                {
                    line[column + 1] = FormatCellValue(cells[row][column]);
                }

                table.AddRow(line);
            }

            table.WriteTo(surface.Out, surface.Format);
        }
        else if (read.Window is { Truncated: true })
        {
            surface.Out.WriteLine("cell data omitted: the sheet exceeds the cell budget");
        }
    }

    private static string FormatCellValue(CellData cell) => cell.V switch
    {
        null => string.Empty,
        bool value => value ? "TRUE" : "FALSE",
        double value => value.ToString(CultureInfo.InvariantCulture),
        string value => value,
        var value => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };
}
