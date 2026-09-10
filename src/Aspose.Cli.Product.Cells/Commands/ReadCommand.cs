using System.CommandLine;
using Aspose.Cli.Product.Cells.Addressing;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// <c>aspose-cli cells query range</c> — step two of the projection ladder: windowed
/// cell data of one sheet. Reads are budgeted (<c>--max-cells</c>) so output
/// stays affordable for agents; over-budget default reads degrade to a
/// summary plus a ready-to-run follow-up command.
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
        };

        var sheetOption = new Option<string?>("--sheet")
        {
            Description = "Sheet to read. Default: the active sheet.",
        };

        var rangeOption = new Option<string?>("--range")
        {
            Description = "Window to read, e.g. A1:F50 or Sales!A1:F50. Default: the used range, subject to --max-cells.",
        };

        // Set only by a generated `next` command: it marks --range as one page
        // of a planned scan of the whole used range, so the chain keeps
        // covering columns the budget could not fit in one window rather than
        // treating those columns as the caller's deliberate choice. Hidden — it
        // is CLI-internal plumbing, not a knob a human sets.
        var continueScan = new Option<bool>("--continue-scan") { Hidden = true };

        var scopeOption = new Option<string>("--scope")
        {
            Description = "Projection scope: values (default), formulas (adds f), styles (adds styleId + pool), full.",
            DefaultValueFactory = _ => ReadScopes.Values,
        };
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
        read.Options.Add(continueScan);
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

            string inputPath = context.Paths.ResolveInput(parseResult.GetRequiredValue(fileArgument));
            WorkbookReadResult result = context.Port.Read(inputPath, new ReadRequest
            {
                SheetName = sheetName,
                Range = range,
                Scope = scope,
                MaxCells = maxCells,
                Password = password.Resolve(parseResult, context.Inputs),
            });

            // The engine returns the projection; the CLI advertises the follow-up
            // command in its own spelling (see NextReadCommand). A scan chain is
            // owned by the CLI whenever it was not started from a caller's own
            // --range: either a budget-summarized default read (no range) or an
            // earlier page of such a scan (--continue-scan).
            bool planned = range is null || parseResult.GetValue(continueScan);
            return result with
            {
                // An explicit range is a complete, bounded request. Only a
                // CLI-planned scan advertises another page.
                Next = planned ? NextReadCommand.Build(inputPath, result, maxCells, windowWasPlanned: true) : null,
            };
        }));

        return read;
    }
}
