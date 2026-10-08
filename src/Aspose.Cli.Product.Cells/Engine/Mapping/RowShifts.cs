using System.Runtime.InteropServices;
using System.Text;
using Aspose.Cells;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Finds the rows one side of a comparison inserted or deleted, which shift the rows below them
/// so that cells compared by address pair different rows. A row whose first cell holds a label,
/// text that no other row of its sheet starts with, is identified by that label, so it matches
/// however its values changed; any other row by its content, a formula by its R1C1 text so that
/// a moved formula still matches. The two sheets' rows are aligned along their longest common
/// subsequence. A run of unmatched rows that is longer on one side and is followed by matched
/// rows inserted or deleted the difference.
/// </summary>
internal sealed class RowShifts
{
    // Bounds the alignment table of the rows between the common first and last rows.
    private const long MaxAlignedPairs = 1_000_000;

    private readonly Dictionary<int, Row> _left = [];
    private readonly Dictionary<int, Row> _right = [];

    /// <summary>Adds one compared address; call in row, then column order.</summary>
    internal void Add(int row, int column, Cell? left, Cell? right)
    {
        Append(_left, row, column, left);
        Append(_right, row, column, right);
    }

    /// <summary>The shifts of sheet <paramref name="sheet"/>, or null when no row shifted.</summary>
    internal Warning? Describe(string sheet, OperationDeadline deadline)
    {
        var ids = new Dictionary<string, int>(StringComparer.Ordinal);
        int[] left = Ids(_left, ids);
        int[] right = Ids(_right, ids);
        int start = 0;
        while (start < left.Length && start < right.Length && left[start] == right[start]) { start++; }
        int leftEnd = left.Length;
        int rightEnd = right.Length;
        while (leftEnd > start && rightEnd > start && left[leftEnd - 1] == right[rightEnd - 1]) { leftEnd--; rightEnd--; }
        int n = leftEnd - start;
        int m = rightEnd - start;
        if ((long)(n + 1) * (m + 1) > MaxAlignedPairs)
        {
            return null;
        }

        // common[i, j]: the longest common subsequence of the rows from start + i and start + j on.
        var common = new int[n + 1, m + 1];
        for (int i = n - 1; i >= 0; i--)
        {
            deadline.ThrowIfExpired("compare-rows");
            for (int j = m - 1; j >= 0; j--)
            {
                common[i, j] = left[start + i] == right[start + j]
                    ? common[i + 1, j + 1] + 1
                    : Math.Max(common[i + 1, j], common[i, j + 1]);
            }
        }

        var shifts = new List<string>();
        int leftGap = 0;
        int rightGap = 0;
        for (int i = 0, j = 0; i < n && j < m;)
        {
            if (left[start + i] == right[start + j])
            {
                AddShift(shifts, start + leftGap, i - leftGap, start + rightGap, j - rightGap);
                leftGap = ++i;
                rightGap = ++j;
            }
            else if (common[i + 1, j] >= common[i, j + 1]) { i++; }
            else { j++; }
        }

        // The last unmatched rows shift only rows that both sides end with.
        if (leftEnd < left.Length)
        {
            AddShift(shifts, start + leftGap, n - leftGap, start + rightGap, m - rightGap);
        }

        return shifts.Count == 0 ? null : new Warning
        {
            Code = CellsDiagnostics.RowsShifted,
            Message = $"Rows of sheet '{sheet}' appear shifted: {string.Join("; ", shifts)}. Cells are compared by address, "
                + "so the cells below a shift are compared with the row that held their address before, and their differences are not edits.",
            Hint = "To compare row for row, apply the same insert_rows or delete_rows to a copy of the left workbook with cells edit --out, and compare that copy.",
            Location = sheet,
        };
    }

    // first: zero-based first row of each side's unmatched run; count: its length.
    private static void AddShift(List<string> shifts, int leftFirst, int leftCount, int rightFirst, int rightCount)
    {
        if (rightCount > leftCount)
        {
            shifts.Add(Describe(rightCount - leftCount, "inserted", "right", rightFirst, rightCount, leftCount == 0));
        }
        else if (leftCount > rightCount)
        {
            shifts.Add(Describe(leftCount - rightCount, "deleted", "left", leftFirst, leftCount, rightCount == 0));
        }
    }

    private static string Describe(int count, string change, string side, int first, int length, bool exact)
    {
        string rows = length == 1 ? $"row {first + 1}" : $"rows {first + 1}-{first + length}";
        return $"{count} row{(count == 1 ? string.Empty : "s")} {change} {(exact ? "at" : "within")} {side} {rows}";
    }

    private static void Append(Dictionary<int, Row> rows, int row, int column, Cell? cell)
    {
        if (cell is null || (cell.Type == CellValueType.IsNull && !cell.IsFormula))
        {
            return;
        }

        // Cells arrive in column order, so the first one creates the row and gives its label.
        Row entry = CollectionsMarshal.GetValueRefOrAddDefault(rows, row, out _) ??= new Row(
            !cell.IsFormula && cell.Type == CellValueType.IsString ? $"{column}\u0001{cell.StringValue}" : null);
        entry.Content.Append(column).Append('\u0001')
            .Append(cell.IsFormula ? cell.R1C1Formula : ComparisonValue.From(cell).ToString()).Append('\u0002');
    }

    // Row r of the result identifies row r by its label when no other row of the sheet has it,
    // otherwise by its content; rows without content share one id.
    private static int[] Ids(Dictionary<int, Row> rows, Dictionary<string, int> ids)
    {
        HashSet<string> unique = [.. rows.Values.Where(static row => row.Label is not null)
            .CountBy(static row => row.Label!).Where(static label => label.Value == 1).Select(static label => label.Key)];
        int[] result = new int[rows.Count == 0 ? 0 : rows.Keys.Max() + 1];
        foreach ((int row, Row entry) in rows)
        {
            string identity = entry.Label is { } label && unique.Contains(label) ? $"\u0003{label}" : entry.Content.ToString();
            if (!ids.TryGetValue(identity, out int id))
            {
                id = ids.Count + 1;
                ids.Add(identity, id);
            }
            result[row] = id;
        }
        return result;
    }

    /// <summary>
    /// One row of one side: its content and its label, the column and text of its first cell
    /// when that cell holds text.
    /// </summary>
    private sealed record Row(string? Label)
    {
        internal StringBuilder Content { get; } = new();
    }
}
