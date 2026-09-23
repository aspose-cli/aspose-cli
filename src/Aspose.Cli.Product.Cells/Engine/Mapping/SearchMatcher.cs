using Aspose.Cells;
using Aspose.Cli.Product.Cells.Addressing;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Finds cells whose display value or formula matches a pattern. Iterates
/// sheet-by-sheet in address order, charging stored cells to the shared work
/// budget and bounding the returned matches separately.
/// </summary>
internal static class SearchMatcher
{
    private const int MaxValueLength = 200;

    /// <summary>Searches every sheet, or only the already resolved <paramref name="sheetIndex"/>.</summary>
    public static (IReadOnlyList<SearchHit> Hits, bool Truncated) Find(
        ResourceBudgetLedger budgets, Workbook workbook, SearchRequest request, int? sheetIndex)
    {
        var hits = new List<SearchHit>();
        bool inValues = request.In is SearchIn.Values or SearchIn.Both;
        bool inFormulas = request.In is SearchIn.Formulas or SearchIn.Both;

        TextSearch query = TextSearch.Create(request.Pattern, request.Regex, request.CaseSensitive);

        foreach (Worksheet sheet in workbook.Worksheets)
        {
            if (sheetIndex is { } only && sheet.Index != only)
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
                    && (query.IsMatch(display)
                        || (rawValue is not null && query.IsMatch(rawValue)));
                bool matched = valueMatched
                    || (inFormulas && formula is not null && query.IsMatch(formula));
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

    private static string Truncate(string value) =>
        value.Length <= MaxValueLength ? value : value[..MaxValueLength] + "…";
}
