using System.Text.RegularExpressions;
using Aspose.Cells;
using Aspose.Cli.Product.Cells.Addressing;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Finds cells whose display value or formula matches a pattern. Iterates
/// sheet-by-sheet in address order, charging stored cells to the shared work
/// budget and bounding the returned matches separately.
/// </summary>
internal static class SearchMatcher
{
    private const int MaxValueLength = 200;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    public static (IReadOnlyList<SearchHit> Hits, bool Truncated) Find(ResourceBudgetLedger budgets, Workbook workbook, SearchRequest request)
    {
        var hits = new List<SearchHit>();
        bool inValues = request.In is SearchIn.Values or SearchIn.Both;
        bool inFormulas = request.In is SearchIn.Formulas or SearchIn.Both;

        Regex? regex = request.Regex
            ? new Regex(
                request.Pattern,
                request.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase,
                RegexTimeout)
            : null;
        StringComparison comparison = request.CaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        foreach (Worksheet sheet in workbook.Worksheets)
        {
            if (request.SheetName is { } name && !string.Equals(sheet.Name, name, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (Cell cell in StoredCells.InAddressOrder(budgets, sheet, "search"))
            {
                budgets.Deadline.ThrowIfExpired("search");
                string display = cell.StringValue ?? string.Empty;
                string? formula = cell.IsFormula ? cell.Formula : null;

                // Match the display text AND the raw value read reports, so a
                // caller searching for 13.75572 finds the cell that shows 13.76.
                string? rawValue = CellMapper.RawValueString(cell);
                bool valueMatched = inValues
                    && (Matches(display, regex, request.Pattern, comparison)
                        || (rawValue is not null && Matches(rawValue, regex, request.Pattern, comparison)));
                bool matched = valueMatched
                    || (inFormulas && formula is not null && Matches(formula, regex, request.Pattern, comparison));
                if (!matched)
                {
                    continue;
                }

                if (hits.Count >= request.MaxHits)
                {
                    return (hits, true);
                }

                hits.Add(new SearchHit
                {
                    Sheet = sheet.Name,
                    Cell = A1.FormatCell(new CellRef(cell.Row, cell.Column)),
                    // Emit the invariant value read reports (ISO date, raw
                    // number), not Aspose's current-culture display — so a
                    // date hit is "2023-05-31" on every machine, deterministic
                    // and agreeing with read/convert, never using the
                    // zh month name the matching already normalized past).
                    Value = Truncate(rawValue ?? display),
                    Formula = formula,
                });
            }
        }

        return (hits, false);
    }

    private static bool Matches(string text, Regex? regex, string pattern, StringComparison comparison) =>
        regex is not null ? regex.IsMatch(text) : text.Contains(pattern, comparison);

    private static string Truncate(string value) =>
        value.Length <= MaxValueLength ? value : value[..MaxValueLength] + "…";
}
