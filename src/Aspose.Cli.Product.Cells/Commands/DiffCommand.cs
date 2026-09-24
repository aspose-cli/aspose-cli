using System.CommandLine;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// <c>cells compare</c> — compares two workbooks. The exit code is 0 even
/// when they differ; the payload's <c>identical</c> flag carries the verdict,
/// so diff doubles as a verifier in scripts and evaluation harnesses.
/// </summary>
internal static class DiffCommand
{
    private const int MinMaxDiffs = 1;
    private const int MaxMaxDiffs = 1_000_000;
    private const string CompareValues = "values";
    private const string CompareFormulas = "formulas";

    public static Command Create(IProductCommandHost<IWorkbookEngine> host)
    {
        var compare = new Option<string>("--compare")
        {
            Description = "What to compare: values, or formulas (values + formulas, the default).",
            DefaultValueFactory = _ => CompareFormulas,
        }.WithInput(InputKind.None);
        compare.AcceptOnlyFromAmong(CompareValues, CompareFormulas);
        var maxDiffsOption = new Option<int>("--max-diffs")
        {
            Description = $"Maximum differing cells to list across the entire workbook ({MinMaxDiffs}-{MaxMaxDiffs}).",
            DefaultValueFactory = _ => 1000,
        };
        return StandardCommand.Create(
            host,
            "compare",
            "Compare stored workbook values and optional formula text; dates use raw serial numbers.",
            new CommandTraits
            {
                Input = new InputDocument("Baseline workbook.", "the baseline file", "left"),
                Other = new InputDocument("Candidate workbook to compare against the baseline.", "the candidate file", "right"),
            },
            [compare, maxDiffsOption],
            (parse, standard) =>
            {
                int maxDiffs = parse.GetValue(maxDiffsOption);
                OptionGuards.EnsureInRange("--max-diffs", maxDiffs, MinMaxDiffs, MaxMaxDiffs,
                    "Lower the budget, or narrow the comparison to the sheets that matter.");
                return standard.OpenEngine().Diff(standard.Input, standard.Other, new DiffRequest
                {
                    Scope = parse.GetValue(compare) == CompareValues ? DiffScope.Values : DiffScope.Formulas,
                    MaxDiffs = maxDiffs,
                    LeftPassword = standard.InputPassword,
                    RightPassword = standard.OtherPassword,
                });
            }).WithExamples(
            [
                "cells compare old.xlsx new.xlsx --output json",
                "cells compare old.xlsx new.xlsx --compare values --max-diffs 50",
            ]);
    }
}
