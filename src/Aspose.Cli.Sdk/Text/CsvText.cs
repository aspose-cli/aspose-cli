using System.Text;

namespace Aspose.Cli.Sdk.Text;

/// <summary>
/// Writes a table as RFC 4180 CSV in UTF-8: one record per row ending in CR LF, one field per cell.
/// Every product that extracts a table writes it here, so the bytes are the same everywhere.
/// </summary>
public static class CsvText
{
    /// <summary>
    /// The rows as CSV bytes, with a UTF-8 byte order mark first when asked, which spreadsheet
    /// programs need to read the file as UTF-8. A row can have any number of fields.
    /// </summary>
    /// <param name="rows">The rows, each its cells' text in order.</param>
    /// <param name="byteOrderMark">Whether the bytes start with the UTF-8 byte order mark.</param>
    public static byte[] Encode(IEnumerable<IEnumerable<string>> rows, bool byteOrderMark)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var csv = new StringBuilder();
        foreach (IEnumerable<string> row in rows)
        {
            csv.AppendJoin(',', row.Select(Field)).Append("\r\n");
        }

        byte[] text = Encoding.UTF8.GetBytes(csv.ToString());
        return byteOrderMark ? [.. Encoding.UTF8.Preamble, .. text] : text;
    }

    /// <summary>
    /// One field: every line break becomes LF, and a field with a comma, a double quote or a line
    /// break is quoted, its double quotes doubled.
    /// </summary>
    /// <param name="value">The cell's text.</param>
    public static string Field(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        return normalized.AsSpan().IndexOfAny(",\"\n") < 0
            ? normalized
            : $"\"{normalized.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
