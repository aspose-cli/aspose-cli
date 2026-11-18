using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// <c>cells compare</c> — compares two workbooks. The exit code is 0 even
/// when they differ; the payload's <c>identical</c> flag carries the verdict,
/// so diff doubles as a verifier in scripts and evaluation harnesses.
/// </summary>
internal static class CompareCommand
{
    private const int MinMaxDiffs = 1;
    private const int MaxMaxDiffs = 1_000_000;
    private const string CompareValues = "values";
    private const string CompareFormulas = "formulas";

    public static CommandDefinition<CellsCompareRequest, CellsCompareResult> Create()
    {
        var compare = new Option<string>("--content")
        {
            Description = "What to compare; formulas adds the formula text to the cell results.",
            DefaultValueFactory = _ => CompareFormulas,
        }.WithInput(InputKind.None);
        compare.AcceptOnlyFromAmong(CompareValues, CompareFormulas);
        var maxDiffsOption = new Option<int>("--max-diffs")
        {
            Description = "Maximum differing cells to list across the entire workbook.",
            DefaultValueFactory = _ => 1000,
        }.InRange(MinMaxDiffs, MaxMaxDiffs);
        return new(
            "compare",
            "Compare stored workbook values and optional formula text; dates are ISO 8601.",
            new CommandTraits
            {
                Input = new InputDocument("Baseline workbook.", "the baseline file", "left"),
                Other = new InputDocument("Candidate workbook to compare against the baseline.", "the candidate file", "right"),
            },
            [compare, maxDiffsOption],
            (parse, standard) =>
            {
                int maxDiffs = parse.GetInRange(maxDiffsOption);
                return new CellsCompareRequest
                {
                    Left = standard.Input,
                    Right = standard.Other,
                    Scope = parse.GetValue(compare) == CompareValues ? CellsCompareScope.Values : CellsCompareScope.Formulas,
                    MaxDiffs = maxDiffs,
                    LeftPassword = standard.InputPassword,
                    RightPassword = standard.OtherPassword,
                };
            })
        {
            Examples =
            [
                "cells compare old.xlsx new.xlsx --output-format json",
                "cells compare old.xlsx new.xlsx --content values --max-diffs 50",
            ],
            Links =
            [
                CommandHelpLink.Docs(CellsModule.Manifest, "verification", "comparing a workbook before and after a change"),
            ],
        };
    }
}
