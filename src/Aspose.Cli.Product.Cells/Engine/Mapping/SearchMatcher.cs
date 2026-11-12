using Aspose.Cells;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Finds the text units of a workbook (<see cref="CellsTextUnits"/>) that match a pattern, in what
/// a reader sees or in the text of formulas: sheet by sheet, cells in address order then the parts
/// outside them, charging stored cells to the shared work budget and collecting one window of
/// matches through the shared search paging.
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
        foreach (CellsTextUnit unit in CellsTextUnits.Of(workbook, sheetIndex, budgets))
        {
            budgets.Deadline.ThrowIfExpired("search");
            // A cell's value as read reports it comes first: the invariant text of a number or date,
            // so a caller searching for 13.75572 finds the cell that shows 13.76.
            if (FirstMatch(request.Query.Text, [.. inValues ? unit.Shown : [], inFormulas ? unit.FormulaText : null]) is not { } match)
            {
                continue;
            }

            bool kept = hits.Offer(() => new CellsSearchHit
            {
                Sheet = unit.Sheet.Name,
                Cell = unit.Address,
                Part = unit.Part?.Token,
                Location = unit.Holder is Cell ? null : unit.Location,
                LinkedCell = unit.LinkedCell,
                Text = match.Source.Substring(match.Start, match.Length),
                Context = TextSearch.Context(match.Source, match.Start, match.Length),
                Formula = unit.FormulaText,
            });
            if (!kept)
            {
                return hits;
            }
        }

        return hits;
    }

    /// <summary>The first match in the first searched text that holds one; null when none does.</summary>
    private static (string Source, int Start, int Length)? FirstMatch(TextSearch query, IEnumerable<string?> sources)
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
