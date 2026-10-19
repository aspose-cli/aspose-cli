using Aspose.Cells;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Finds cells whose display value or formula matches a pattern. Iterates
/// sheet-by-sheet in address order, charging stored cells to the shared work
/// budget and collecting one window of matches through the shared search paging.
/// </summary>
internal static class SearchMatcher
{
    /// <summary>Searches every sheet, or only the already resolved <paramref name="sheetIndex"/>.</summary>
    public static SearchHits<CellsSearchHit> Find(
        ResourceBudgetLedger budgets, Workbook workbook, CellsSearchRequest request, int? sheetIndex)
    {
        SearchHits<CellsSearchHit> hits = request.Query.Collect<CellsSearchHit>();
        bool inValues = request.In is CellsSearchIn.Values or CellsSearchIn.All;
        bool inFormulas = request.In is CellsSearchIn.Formulas or CellsSearchIn.All;
        TextSearch query = request.Query.Text;

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

                // Match the raw value read reports AND the display text, so a caller searching
                // for 13.75572 finds the cell that shows 13.76. The raw value comes first: it is
                // the invariant text read reports (ISO date, raw number), the same on every
                // machine, never Aspose's current-culture display.
                string? rawValue = CellMapper.RawValueString(cell);
                if (FirstMatch(
                        query,
                        inValues ? rawValue ?? display : null,
                        inValues ? display : null,
                        inFormulas ? formula : null) is not { } match)
                {
                    continue;
                }

                bool kept = hits.Offer(() => new CellsSearchHit
                {
                    Sheet = sheet.Name,
                    Cell = A1.FormatCell(new CellRef(cell.Row, cell.Column)),
                    Text = match.Source.Substring(match.Start, match.Length),
                    Context = TextSearch.Context(match.Source, match.Start, match.Length),
                    Formula = formula,
                });
                if (!kept)
                {
                    return hits;
                }
            }
        }

        return hits;
    }

    /// <summary>The first match in the first searched text that holds one; null when none does.</summary>
    private static (string Source, int Start, int Length)? FirstMatch(TextSearch query, params string?[] sources)
    {
        foreach (string? source in sources)
        {
            if (source is not null && query.Find(source) is [var (start, length), ..])
            {
                return (source, start, length);
            }
        }

        return null;
    }
}
