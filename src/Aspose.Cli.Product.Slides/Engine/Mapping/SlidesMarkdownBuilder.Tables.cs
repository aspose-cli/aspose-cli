using System.Drawing;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;
using Aspose.Slides;

namespace Aspose.Cli.Product.Slides.Engine.Mapping;

// GitHub-flavoured pipe tables: a header row, a delimiter row, then body rows.
internal static partial class SlidesMarkdownBuilder
{
    // PowerPoint's default row height (0.4 in), which fits one line of 18 pt text.
    private const double DefaultRowHeight = 28.8;

    // A table may end this far below its area before it is reported.
    private const double FitTolerance = 0.5;

    /// <summary>
    /// Reads the pipe table that starts at <paramref name="index"/> and moves the index to
    /// its last row, or returns null when the line is not followed by a delimiter row with
    /// as many cells. Body rows continue while lines hold an unescaped pipe.
    /// </summary>
    private static MarkdownTable? ReadTable(string[] lines, ref int index, string title)
    {
        if (index + 1 >= lines.Length
            || !HasPipe(lines[index])
            || !lines[index + 1].Contains('|', StringComparison.Ordinal)
            || !DelimiterPattern().IsMatch(lines[index + 1].Trim()))
        {
            return null;
        }

        List<string> header = Cells(lines[index]);
        List<string> delimiter = Cells(lines[index + 1]);
        if (header.Count != delimiter.Count)
        {
            return null;
        }

        var rows = new List<IReadOnlyList<IReadOnlyList<AuthoredRun>>> { Row(header, header.Count) };
        int next = index + 2;
        while (next < lines.Length && IsBodyRow(lines[next]))
        {
            rows.Add(Row(Cells(lines[next]), header.Count));
            next++;
        }

        if (header.Count > SlidesInsertTableOp.MaxCols || rows.Count > SlidesInsertTableOp.MaxRows)
        {
            throw new CliException(
                ErrorCodes.FeatureUnsupported,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The Markdown table under '{title}' has {rows.Count} rows and {header.Count} columns; a slide table holds at most {SlidesInsertTableOp.MaxRows} rows and {SlidesInsertTableOp.MaxCols} columns."),
                hint: "Split the table into several tables under their own headings.");
        }

        index = next - 1;
        return new MarkdownTable(delimiter.Select(Alignment).ToArray(), rows);
    }

    private static bool IsBodyRow(string line) =>
        !string.IsNullOrWhiteSpace(line)
        && HasPipe(line)
        && !line.TrimStart().StartsWith("```", StringComparison.Ordinal)
        && !HeadingPattern().IsMatch(line.TrimEnd());

    private static bool HasPipe(string line) => Split(line).Count > 1;

    /// <summary>The cells of one row, without the optional outer pipes.</summary>
    private static List<string> Cells(string line)
    {
        List<string> cells = Split(line.Trim());
        if (cells.Count > 1 && cells[0].Length == 0)
        {
            cells.RemoveAt(0);
        }

        if (cells.Count > 1 && cells[^1].Length == 0)
        {
            cells.RemoveAt(cells.Count - 1);
        }

        return cells;
    }

    /// <summary>
    /// Splits a line at its unescaped pipes. A backslash escapes only the next character:
    /// <c>\|</c> is a literal pipe, and in <c>\\|</c> the pipe still ends the cell. Other
    /// escaped pairs stay in the text as written.
    /// </summary>
    private static List<string> Split(string line)
    {
        var cells = new List<string>();
        var cell = new StringBuilder();
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '\\' && i + 1 < line.Length)
            {
                i++;
                if (line[i] != '|')
                {
                    cell.Append('\\');
                }

                cell.Append(line[i]);
            }
            else if (line[i] == '|')
            {
                cells.Add(cell.ToString().Trim());
                cell.Clear();
            }
            else
            {
                cell.Append(line[i]);
            }
        }

        cells.Add(cell.ToString().Trim());
        return cells;
    }

    // A ragged row is padded with empty cells or cut to the header's column count.
    private static IReadOnlyList<IReadOnlyList<AuthoredRun>> Row(List<string> cells, int columns) =>
        Enumerable.Range(0, columns)
            .Select(column => Inline(column < cells.Count ? cells[column] : string.Empty))
            .ToArray();

    private static TextAlignment Alignment(string delimiter) =>
        (delimiter.StartsWith(':'), delimiter.EndsWith(':')) switch
        {
            (true, true) => TextAlignment.Center,
            (false, true) => TextAlignment.Right,
            (true, false) => TextAlignment.Left,
            _ => TextAlignment.NotDefined,
        };

    /// <summary>
    /// Places the table in the first content placeholder that holds no text, full width at
    /// its top, and reports a table that ends below that area.
    /// </summary>
    private static Warning? AddTable(ISlide slide, int number, MarkdownTable source, IAutoShape[] content, bool besideText)
    {
        IAutoShape? frame = content.FirstOrDefault(static shape => string.IsNullOrEmpty(shape.TextFrame?.Text));
        RectangleF box = frame is null
            ? SlidesAuthoring.Canvas(
                slide,
                besideText ? new RectangleF(0.55f, 0.25f, 0.38f, 0.62f) : new RectangleF(0.07f, 0.25f, 0.86f, 0.65f))
            : new RectangleF(frame.X, frame.Y, frame.Width, frame.Height);
        int rows = source.Rows.Count;
        int columns = source.Alignments.Count;
        ITable table = SlidesAuthoring.AddTable(
            slide,
            box.X,
            box.Y,
            box.Width,
            Math.Min(box.Height, rows * DefaultRowHeight),
            rows,
            columns);
        table.Name = "Table";
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                ITextFrame cell = table[column, row].TextFrame;
                SlidesAuthoring.WriteParagraphs(cell, [new AuthoredParagraph(source.Rows[row][column], 0, ParagraphList.Inherit)]);
                if (source.Alignments[column] != TextAlignment.NotDefined)
                {
                    cell.Paragraphs[0].ParagraphFormat.Alignment = source.Alignments[column];
                }
            }
        }

        if (frame is not null)
        {
            slide.Shapes.Remove(frame);
        }

        // Rows grow with their text at the table style's font size, so the laid-out height
        // shows whether the table stays readable inside its area.
        return table.Height <= box.Height + FitTolerance
            ? null
            : new Warning
            {
                Code = SlidesDiagnostics.TableOverflow,
                Message = string.Create(
                    CultureInfo.InvariantCulture,
                    $"The table on slide {number} is {table.Height:0} pt high but its area is {box.Height:0} pt, so it runs past the area."),
                Hint = "Split the table across slides under the same heading or shorten its cells, then review the slide.",
                Location = string.Create(CultureInfo.InvariantCulture, $"slide {number}"),
            };
    }

    // |---|:--:|--:| with optional outer pipes.
    [GeneratedRegex(@"^\|?\s*:?-+:?\s*(?:\|\s*:?-+:?\s*)*\|?$", RegexOptions.CultureInvariant)]
    private static partial Regex DelimiterPattern();
}

/// <summary>A pipe table: the header is the first row, and each column has its alignment.</summary>
internal sealed record MarkdownTable(
    IReadOnlyList<TextAlignment> Alignments,
    IReadOnlyList<IReadOnlyList<IReadOnlyList<AuthoredRun>>> Rows);
