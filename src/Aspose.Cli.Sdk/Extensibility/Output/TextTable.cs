using System.Text;

namespace Aspose.Cli.Sdk.Extensibility.Output;

/// <summary>Rendering flavor of a <see cref="TextTable"/>.</summary>
public enum TableFormat
{
    /// <summary>Space-aligned columns.</summary>
    Plain,

    /// <summary>Markdown pipe table.</summary>
    Markdown,
}

/// <summary>
/// Minimal fixed-width table renderer for human output. Deliberately plain:
/// no colors, no box drawing — output must stay cheap to read in agent
/// transcripts and diff-friendly in terminals.
/// </summary>
public sealed class TextTable
{
    private const string ColumnSeparator = "  ";

    private readonly string[] _headers;
    private readonly List<string[]> _rows = [];

    /// <summary>Creates a table with a fixed ordered set of headers.</summary>
    public TextTable(params string[] headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        _headers = headers;
    }

    /// <summary>Adds one row whose cell count must match the headers.</summary>
    public void AddRow(params string[] cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        if (cells.Length != _headers.Length)
        {
            throw new ArgumentException(
                $"Row has {cells.Length} cells but the table has {_headers.Length} columns.", nameof(cells));
        }

        _rows.Add(cells);
    }

    /// <summary>Writes the complete table in the selected deterministic format.</summary>
    public void WriteTo(TextWriter writer, TableFormat format = TableFormat.Plain)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (format == TableFormat.Markdown)
        {
            WriteMarkdown(writer);
            return;
        }

        int[] widths = new int[_headers.Length];
        for (int column = 0; column < _headers.Length; column++)
        {
            widths[column] = _headers[column].Length;
            foreach (string[] row in _rows)
            {
                widths[column] = Math.Max(widths[column], row[column].Length);
            }
        }

        writer.WriteLine(FormatRow(_headers, widths));
        foreach (string[] row in _rows)
        {
            writer.WriteLine(FormatRow(row, widths));
        }
    }

    private void WriteMarkdown(TextWriter writer)
    {
        writer.WriteLine(MarkdownRow(_headers));
        writer.WriteLine("|" + string.Concat(Enumerable.Repeat(" --- |", _headers.Length)));
        foreach (string[] row in _rows)
        {
            writer.WriteLine(MarkdownRow(row));
        }
    }

    private static string MarkdownRow(string[] cells) =>
        "| " + string.Join(" | ", cells.Select(static cell =>
            cell.Replace("|", "\\|", StringComparison.Ordinal))) + " |";

    private static string FormatRow(string[] cells, int[] widths)
    {
        var builder = new StringBuilder();
        for (int column = 0; column < cells.Length; column++)
        {
            if (column > 0)
            {
                builder.Append(ColumnSeparator);
            }

            // The last column is not padded to avoid trailing whitespace.
            builder.Append(column == cells.Length - 1
                ? cells[column]
                : cells[column].PadRight(widths[column]));
        }

        return builder.ToString();
    }
}
