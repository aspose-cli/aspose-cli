using System.CommandLine;
using Aspose.Cli.Product.Cells.Addressing;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// <c>aspose-cli cells query range</c> — step two of the projection ladder: windowed
/// cell data of one sheet. Reads are budgeted (<c>--max-cells</c>) so output
/// stays affordable for agents; a default read over budget degrades to a summary plus a
/// ready-to-run follow-up command, and an explicit range over budget is refused with the
/// command that scans it page by page.
/// </summary>
internal static class ReadCommand
{
    private const int MinMaxCells = 1;
    private const int MaxMaxCells = 1_000_000;

    public static Command Create(IProductCommandHost<IWorkbookEngine> host)
    {
        var fileArgument = new Argument<string>("file")
        {
            Description = "Workbook to read.",
        }.WithInput(InputKind.File);

        var sheetOption = new Option<string?>("--sheet")
        {
            Description = "Sheet to read. Default: the active sheet.",
        }.WithInput(InputKind.None);

        var rangeOption = new Option<string?>("--range")
        {
            Description = "Window to read, e.g. A1:F50 or Sales!A1:F50. Default: the used range, subject to --max-cells.",
        }.WithInput(InputKind.None);

        // Set only by a generated `next` command: it names the region a planned scan
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

        var password = new PasswordOptions("--password", "the workbook");

        var read = new Command("range", "Read cell data of one sheet as a windowed projection.");
        read.Arguments.Add(fileArgument);
        read.Options.Add(sheetOption);
        read.Options.Add(rangeOption);
        read.Options.Add(scanOption);
        read.Options.Add(scopeOption);
        read.Options.Add(maxCellsOption);
        password.AddTo(read);

        read.SetAction(parseResult => host.Run(parseResult, context =>
        {
            int maxCells = parseResult.GetValue(maxCellsOption);
            OptionGuards.EnsureInRange("--max-cells", maxCells, MinMaxCells, MaxMaxCells,
                "Keep the budget modest; page through large sheets with the 'next' commands instead.");

            _ = ReadScopeExtensions.TryParse(parseResult.GetValue(scopeOption), out ReadScope scope);

            (string? sheetName, RangeRef? range) = SheetRangeInput.Resolve(
                parseResult.GetValue(sheetOption), parseResult.GetValue(rangeOption));
            RangeRef? scan = parseResult.GetValue(scanOption) is { } region ? A1.ParseRange(region).Range : null;

            string inputPath = context.Paths.ResolveInput(parseResult.GetRequiredValue(fileArgument));
            if (range is { } explicitRange && explicitRange.CellCount > maxCells)
            {
                throw CellsErrors.RangeTooLarge(
                    explicitRange.CellCount,
                    maxCells,
                    "Scan the range in budgeted windows: run "
                        + NextReadCommand.First(inputPath, sheetName, explicitRange, scope.ToContractName(), maxCells)
                        + " and follow each 'next' command, or raise --max-cells.");
            }

            WorkbookReadResult result = context.Port.Read(inputPath, new ReadRequest
            {
                SheetName = sheetName,
                Range = range,
                Scope = scope,
                MaxCells = maxCells,
                Password = password.Resolve(parseResult, context.Inputs, context.ReadEnvironment),
            });

            // An explicit range is a complete, bounded request. Only a CLI-planned scan
            // advertises another page: a default read scans the used range, and a generated
            // page scans the region its command carries.
            RangeRef? scanned = scan
                ?? (range is null && result.Sheet.UsedRange is { } used ? A1.ParseRange(used).Range : null);
            return result with
            {
                Next = scanned is { } bounds ? NextReadCommand.Build(inputPath, result, maxCells, bounds) : null,
            };
        }));

        return read;
    }
}
