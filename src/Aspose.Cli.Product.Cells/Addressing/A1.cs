using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Addressing;

/// <summary>
/// Parser and formatter for A1 notation. Implemented in the core on purpose:
/// range syntax is part of the public CLI contract and must not depend on any
/// engine SDK's interpretation.
/// </summary>
/// <remarks>
/// Supported forms: <c>C5</c>, <c>A1:C10</c>, <c>Sales!A1:C10</c>,
/// <c>'My Sheet'!A1:C10</c> (single quotes escaped by doubling), and absolute
/// markers (<c>$A$1</c>) which are accepted and normalized away.
/// Whole-row and whole-column specs (<c>A:A</c>, <c>1:3</c>) are rejected;
/// explicit bounds keep output sizes predictable for agents.
/// </remarks>
internal static class A1
{
    /// <summary>Maximum number of rows in a worksheet (Excel limit).</summary>
    public const int MaxRows = 1_048_576;

    /// <summary>Maximum number of columns in a worksheet (Excel limit, column XFD).</summary>
    public const int MaxColumns = 16_384;

    /// <summary>Parses a range specification, optionally sheet-qualified.</summary>
    /// <exception cref="CliException"><c>RANGE_INVALID</c> when the spec cannot be parsed.</exception>
    public static RangeSpec ParseRange(string spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        string trimmed = spec.Trim();
        if (trimmed.Length == 0)
        {
            throw CellsErrors.RangeInvalid(spec, "the range is empty");
        }

        (string? sheetName, string rangePart) = SplitSheet(spec, trimmed);
        if (rangePart.Length == 0)
        {
            throw CellsErrors.RangeInvalid(spec, "missing cell range after the sheet name");
        }

        string[] corners = rangePart.Split(':');
        if (corners.Length > 2)
        {
            throw CellsErrors.RangeInvalid(spec, "a range has at most one ':' separator");
        }

        CellRef start = ParseCellPart(spec, corners[0]);
        CellRef end = corners.Length == 2 ? ParseCellPart(spec, corners[1]) : start;
        return new RangeSpec(sheetName, new RangeRef(start, end));
    }

    /// <summary>Parses a single cell reference such as <c>B3</c> or <c>$B$3</c>.</summary>
    /// <exception cref="CliException"><c>RANGE_INVALID</c> when the text is not a cell.</exception>
    public static CellRef ParseCell(string cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        return ParseCellPart(cell, cell.Trim());
    }

    /// <summary>Parses column letters (e.g. <c>B</c>, <c>AA</c>) to a zero-based column index.</summary>
    /// <exception cref="CliException"><c>RANGE_INVALID</c> when the text is not a column.</exception>
    public static int ParseColumn(string letters)
    {
        ArgumentNullException.ThrowIfNull(letters);
        string trimmed = letters.Trim();
        if (trimmed.Length == 0 || !trimmed.All(char.IsAsciiLetter))
        {
            throw CellsErrors.RangeInvalid(letters, $"'{letters}' is not a column; give letters such as B or AA");
        }

        // Anchor the letters to row 1 and reuse the cell parser, so column
        // bounds and error reporting stay identical to a full cell reference.
        return ParseCell(trimmed + "1").Column;
    }

    /// <summary>
    /// Parses a whole-row band such as <c>1:3</c>, <c>$1:$3</c> or <c>2</c> into its
    /// absolute form <c>$1:$3</c>, the only place whole rows are addressable.
    /// </summary>
    /// <exception cref="CliException"><c>RANGE_INVALID</c> when the text is not a row band.</exception>
    public static string ParseRowBand(string band) => ParseBand(band, part =>
    {
        if (!int.TryParse(part, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int row)
            || row < 1 || row > MaxRows)
        {
            throw CellsErrors.RangeInvalid(band, $"'{part}' is not a row number in 1..{MaxRows}");
        }

        return row;
    }, static row => row.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>
    /// Parses a whole-column band such as <c>A:B</c>, <c>$A:$B</c> or <c>C</c> into its
    /// absolute form <c>$A:$B</c>.
    /// </summary>
    /// <exception cref="CliException"><c>RANGE_INVALID</c> when the text is not a column band.</exception>
    public static string ParseColumnBand(string band) => ParseBand(band, ParseColumn, ColumnName);

    private static string ParseBand(string band, Func<string, int> parse, Func<int, string> format)
    {
        ArgumentNullException.ThrowIfNull(band);
        string[] parts = band.Split(':');
        if (parts.Length > 2)
        {
            throw CellsErrors.RangeInvalid(band, "a band has at most one ':' separator");
        }

        int first = parse(StripAbsolute(parts[0]));
        int last = parts.Length == 2 ? parse(StripAbsolute(parts[1])) : first;
        return $"${format(Math.Min(first, last))}:${format(Math.Max(first, last))}";

        static string StripAbsolute(string part)
        {
            string trimmed = part.Trim();
            return trimmed.StartsWith('$') ? trimmed[1..] : trimmed;
        }
    }

    /// <summary>Formats a zero-based column index as letters (0 becomes <c>A</c>).</summary>
    public static string ColumnName(int columnIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(columnIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(columnIndex, MaxColumns);

        Span<char> buffer = stackalloc char[3];
        int position = buffer.Length;
        int remaining = columnIndex;
        do
        {
            buffer[--position] = (char)('A' + remaining % 26);
            remaining = remaining / 26 - 1;
        }
        while (remaining >= 0);

        return new string(buffer[position..]);
    }

    /// <summary>Formats a cell in A1 notation.</summary>
    public static string FormatCell(CellRef cell) => $"{ColumnName(cell.Column)}{cell.Row + 1}";

    /// <summary>Formats a range in A1 notation; single-cell ranges collapse to one cell.</summary>
    public static string FormatRange(RangeRef range) =>
        range.Start == range.End
            ? FormatCell(range.Start)
            : $"{FormatCell(range.Start)}:{FormatCell(range.End)}";

    private static (string? SheetName, string RangePart) SplitSheet(string original, string spec)
    {
        // Sheet names cannot contain '!', so the last '!' separates the parts.
        int separator = spec.LastIndexOf('!');
        if (separator < 0)
        {
            return (null, spec);
        }

        string sheetPart = spec[..separator].Trim();
        string rangePart = spec[(separator + 1)..].Trim();

        if (sheetPart.Length == 0)
        {
            throw CellsErrors.RangeInvalid(original, "missing sheet name before '!'");
        }

        if (sheetPart.Length >= 2 && sheetPart[0] == '\'' && sheetPart[^1] == '\'')
        {
            // Quoted sheet name; embedded quotes are escaped by doubling.
            string unquoted = sheetPart[1..^1].Replace("''", "'", StringComparison.Ordinal);
            if (unquoted.Length == 0)
            {
                throw CellsErrors.RangeInvalid(original, "the quoted sheet name is empty");
            }

            return (unquoted, rangePart);
        }

        return (sheetPart, rangePart);
    }

    private static CellRef ParseCellPart(string original, string part)
    {
        ReadOnlySpan<char> span = part.AsSpan().Trim();
        if (span.Length == 0)
        {
            throw CellsErrors.RangeInvalid(original, "the cell reference is empty");
        }

        int position = 0;
        if (span[position] == '$')
        {
            position++;
        }

        int columnStart = position;
        while (position < span.Length && char.IsAsciiLetter(span[position]))
        {
            position++;
        }

        int letterCount = position - columnStart;
        if (letterCount is 0)
        {
            throw CellsErrors.RangeInvalid(original, $"'{part}' does not start with a column letter");
        }

        if (letterCount > 3)
        {
            throw CellsErrors.RangeInvalid(original, $"column '{span[columnStart..position]}' is beyond the last column XFD");
        }

        int column = 0;
        foreach (char letter in span[columnStart..position])
        {
            column = column * 26 + (char.ToUpperInvariant(letter) - 'A' + 1);
        }

        column--;
        if (column >= MaxColumns)
        {
            throw CellsErrors.RangeInvalid(original, $"column '{span[columnStart..position]}' is beyond the last column XFD");
        }

        if (position < span.Length && span[position] == '$')
        {
            position++;
        }

        ReadOnlySpan<char> rowDigits = span[position..];
        if (rowDigits.Length == 0)
        {
            throw CellsErrors.RangeInvalid(original, $"'{part}' is missing a row number (whole-column ranges are not supported; give explicit bounds like A1:A100)");
        }

        if (!int.TryParse(rowDigits, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int row1Based))
        {
            throw CellsErrors.RangeInvalid(original, $"'{part}' has an invalid row number '{rowDigits}'");
        }

        if (row1Based < 1 || row1Based > MaxRows)
        {
            throw CellsErrors.RangeInvalid(original, $"row {row1Based} is outside 1..{MaxRows}");
        }

        return new CellRef(row1Based - 1, column);
    }
}
