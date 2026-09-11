using System.CommandLine;
using System.Text.RegularExpressions;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// <c>aspose-cli cells query search</c> — finds cells whose value or formula matches a
/// pattern, without dumping whole sheets. Hits are budgeted (<c>--max-hits</c>)
/// so a search over a 40-sheet workbook stays affordable for an agent.
/// </summary>
internal static class SearchCommand
{
    private const int MinMaxHits = 1;
    private const int MaxMaxHits = 10_000;
    private const string InValues = "values";
    private const string InFormulas = "formulas";
    private const string InBoth = "both";

    public static Command Create(IProductCommandHost<IWorkbookEngine> host)
    {
        var fileArgument = new Argument<string>("file") { Description = "Workbook to search." }.WithInput(InputKind.File);
        var patternArgument = new Argument<string>("pattern")
        {
            Description = "Text to find, or a regular expression with --regex.",
        }.WithInput(InputKind.None);

        var regexOption = new Option<bool>("--regex") { Description = "Treat the pattern as a regular expression." };
        var inOption = new Option<string>("--in")
        {
            Description = "Where to search: values (default), formulas or both.",
            DefaultValueFactory = _ => InValues,
        }.WithInput(InputKind.None);
        inOption.AcceptOnlyFromAmong(InValues, InFormulas, InBoth);
        var sheetOption = new Option<string?>("--sheet") { Description = "Restrict to one sheet. Default: all sheets." }.WithInput(InputKind.None);
        var maxHitsOption = new Option<int>("--max-hits")
        {
            Description = $"Maximum hits ({MinMaxHits}-{MaxMaxHits}).",
            DefaultValueFactory = _ => 100,
        };
        var caseOption = new Option<bool>("--case-sensitive") { Description = "Match case exactly." };
        var password = new PasswordOptions("--password", "the workbook");

        var search = new Command("search", "Find cells whose value or formula matches a pattern.");
        search.Arguments.Add(fileArgument);
        search.Arguments.Add(patternArgument);
        search.Options.Add(regexOption);
        search.Options.Add(inOption);
        search.Options.Add(sheetOption);
        search.Options.Add(maxHitsOption);
        search.Options.Add(caseOption);
        password.AddTo(search);

        search.SetAction(parseResult => host.Run(parseResult, context =>
        {
            int maxHits = parseResult.GetValue(maxHitsOption);
            OptionGuards.EnsureInRange("--max-hits", maxHits, MinMaxHits, MaxMaxHits,
                "Keep the budget modest; narrow the search with --sheet.");

            string pattern = parseResult.GetRequiredValue(patternArgument);
            bool regex = parseResult.GetValue(regexOption);
            if (regex)
            {
                try
                {
                    _ = Regex.Match(string.Empty, pattern);
                }
                catch (ArgumentException ex)
                {
                    throw CliErrors.OptionInvalid(
                        "pattern",
                        $"invalid regular expression: {ex.Message}",
                        "Fix the expression, or drop --regex for a literal search.");
                }
            }

            SearchIn scope = parseResult.GetValue(inOption) switch
            {
                InFormulas => SearchIn.Formulas,
                InBoth => SearchIn.Both,
                _ => SearchIn.Values,
            };

            string inputPath = context.Paths.ResolveInput(parseResult.GetRequiredValue(fileArgument));
            return context.Port.Search(inputPath, new SearchRequest
            {
                Pattern = pattern,
                Regex = regex,
                In = scope,
                SheetName = parseResult.GetValue(sheetOption),
                MaxHits = maxHits,
                CaseSensitive = parseResult.GetValue(caseOption),
                Password = password.Resolve(parseResult, context.Inputs),
            });
        }));

        return search;
    }
}
