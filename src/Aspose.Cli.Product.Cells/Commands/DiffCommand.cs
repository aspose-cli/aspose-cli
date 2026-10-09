using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility.Output;

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

    public static CommandDefinition<DiffRequest, DiffResult> Create()
    {
        var compare = new Option<string>("--compare")
        {
            Description = "What to compare: values, or formulas (values + formulas).",
            DefaultValueFactory = _ => CompareFormulas,
        }.WithInput(InputKind.None);
        compare.AcceptOnlyFromAmong(CompareValues, CompareFormulas);
        var maxDiffsOption = new Option<int>("--max-diffs")
        {
            Description = $"Maximum differing cells to list across the entire workbook ({MinMaxDiffs}-{MaxMaxDiffs}).",
            DefaultValueFactory = _ => 1000,
        };
        return new(
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
                return new DiffRequest
                {
                    Left = standard.Input,
                    Right = standard.Other,
                    Scope = parse.GetValue(compare) == CompareValues ? DiffScope.Values : DiffScope.Formulas,
                    MaxDiffs = maxDiffs,
                    LeftPassword = standard.InputPassword,
                    RightPassword = standard.OtherPassword,
                };
            },
            Table)
        {
            Examples =
            [
                "cells compare old.xlsx new.xlsx --output json",
                "cells compare old.xlsx new.xlsx --compare values --max-diffs 50",
            ],
        };
    }

    internal static void Table(DiffResult diff, TableSurface surface)
    {
        if (diff.Identical)
        {
            surface.Out.WriteLine($"identical: {diff.Left.Path} == {diff.Right.Path} (within scope)");
            return;
        }

        DiffSummary summary = diff.Summary;
        surface.Out.WriteLine(
            $"{diff.Left.Path} vs {diff.Right.Path}: {summary.CellsDiffering} cell(s) differ across " +
            $"{summary.SheetsModified} sheet(s); +{summary.SheetsAdded} -{summary.SheetsRemoved} sheet(s), {summary.SheetsRenamed} renamed");

        if (diff.Sheets is { Count: > 0 } sheets)
        {
            surface.Out.WriteLine();
            var table = new TextTable("sheet", "status", "cells");
            foreach (SheetDiff sheet in sheets)
            {
                table.AddRow(sheet.Name, sheet.From is null ? sheet.Status : $"renamed from {sheet.From}", sheet.Cells is { } changed ? TableText.Int(changed.Count) : "-");
            }

            table.WriteTo(surface.Out, surface.Format);
        }
    }
}
