using System.CommandLine;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// <c>aspose-cli cells compare</c> — compares two workbooks. The exit code is 0 even
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
        var leftArgument = new Argument<string>("left") { Description = "Baseline workbook." };
        var rightArgument = new Argument<string>("right")
        {
            Description = "Candidate workbook to compare against the baseline.",
        };

        var compareOption = new Option<string>("--compare")
        {
            Description = "What to compare: values, or formulas (values + formulas, the default).",
            DefaultValueFactory = _ => CompareFormulas,
        };
        compareOption.AcceptOnlyFromAmong(CompareValues, CompareFormulas);

        var maxDiffsOption = new Option<int>("--max-diffs")
        {
            Description = $"Maximum differing cells to list ({MinMaxDiffs}-{MaxMaxDiffs}).",
            DefaultValueFactory = _ => 1000,
        };

        var leftPassword = new PasswordOptions("--left-password", "the baseline file", allowStdin: false);
        var rightPassword = new PasswordOptions("--right-password", "the candidate file", allowStdin: false);

        var diff = new Command("compare", "Compare two workbooks (values and, optionally, formulas).");
        diff.Arguments.Add(leftArgument);
        diff.Arguments.Add(rightArgument);
        diff.Options.Add(compareOption);
        diff.Options.Add(maxDiffsOption);
        leftPassword.AddTo(diff);
        rightPassword.AddTo(diff);

        diff.SetAction(parseResult => host.Run(parseResult, context =>
        {
            int maxDiffs = parseResult.GetValue(maxDiffsOption);
            OptionGuards.EnsureInRange("--max-diffs", maxDiffs, MinMaxDiffs, MaxMaxDiffs,
                "Lower the budget, or narrow the comparison to the sheets that matter.");

            DiffScope scope = parseResult.GetValue(compareOption) == CompareValues
                ? DiffScope.Values
                : DiffScope.Formulas;

            string leftPath = context.Paths.ResolveInput(parseResult.GetRequiredValue(leftArgument));
            string rightPath = context.Paths.ResolveInput(parseResult.GetRequiredValue(rightArgument));
            return context.Port.Diff(leftPath, rightPath, new DiffRequest
            {
                Scope = scope,
                MaxDiffs = maxDiffs,
                LeftPassword = leftPassword.Resolve(parseResult, context.Inputs),
                RightPassword = rightPassword.Resolve(parseResult, context.Inputs),
            });
        }));

        return diff;
    }
}
