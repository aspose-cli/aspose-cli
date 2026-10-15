using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Fields;
using Aspose.Words.Markup;
using Aspose.Words.Notes;
using Aspose.Words.Tables;

namespace Aspose.Cli.Product.Words.Engine.Editing;

/// <summary>
/// The state of a part of a document as one text that differs exactly when an operation changed
/// what the part shows or stores: its text, the character, paragraph, list, table and row
/// formatting, the revisions, fields, bookmarks, comments and notes it holds, and each image by
/// a hash of its content. Snapshots compare these states (<see cref="WordsSnapshots"/>).
/// </summary>
internal static class WordsDigest
{
    /// <summary>The state of a node and everything it holds, as a SHA-256 hex digest.</summary>
    internal static string Of(Node node)
    {
        var text = new StringBuilder();
        Append(text, node);
        return Hash(text);
    }

    /// <summary>
    /// The state of a section without its body: its page setup and its headers and footers. In
    /// <paramref name="evaluation"/> mode the marks evaluation mode writes into them, its footer
    /// sentence and its watermark, are no state: it writes them again at every save.
    /// </summary>
    internal static string OfSection(Section section, bool evaluation)
    {
        var text = new StringBuilder();
        PageSetup setup = section.PageSetup;
        Join(text, setup.PageWidth, setup.PageHeight, setup.Orientation, setup.TopMargin, setup.RightMargin, setup.BottomMargin,
            setup.LeftMargin, setup.HeaderDistance, setup.FooterDistance, setup.TextColumns.Count, setup.SectionStart,
            setup.DifferentFirstPageHeaderFooter, setup.OddAndEvenPagesHeaderFooter, setup.RestartPageNumbering,
            setup.PageStartingNumber, setup.PageNumberStyle);
        foreach (HeaderFooter headerFooter in section.HeadersFooters.Cast<HeaderFooter>().OrderBy(static item => item.HeaderFooterType))
        {
            text.Append('[').Append(headerFooter.HeaderFooterType).Append(']');
            Append(text, headerFooter, evaluation);
        }

        return Hash(text);
    }

    /// <summary>
    /// What a read does not report of a style (<see cref="Mapping.WordsReadBack.Styles"/>): its next and linked
    /// style, the rest of its font, its color as stored, and its paragraph format, read for paragraph styles only.
    /// </summary>
    internal static string OfStyleBeyondRead(Style style)
    {
        var text = new StringBuilder();
        Join(text, style.NextParagraphStyleName, style.LinkedStyleName);
        // A list style has no font or paragraph format of its own.
        if (style.Font is { } font)
        {
            Join(text, font.Italic, font.Underline, font.Color.ToArgb(), font.HighlightColor.ToArgb(), font.StrikeThrough, font.StyleName);
        }

        if (style.ParagraphFormat is { } format)
        {
            Paragraph(text, format);
        }

        return Hash(text);
    }

    /// <summary>The SHA-256 hex digest of bytes, such as an image's content.</summary>
    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static string Hash(StringBuilder text) => Hash(Encoding.UTF8.GetBytes(text.ToString()));

    private static void Append(StringBuilder text, Node node, bool evaluation = false)
    {
        text.Append('<').Append((int)node.NodeType);
        switch (node)
        {
            case Paragraph paragraph:
                Paragraph(text, paragraph.ParagraphFormat);
                Join(text, paragraph.ListFormat.IsListItem ? paragraph.ListFormat.List.ListId : -1, paragraph.ListFormat.ListLevelNumber,
                    paragraph.IsInsertRevision, paragraph.IsDeleteRevision, paragraph.IsFormatRevision, paragraph.IsMoveFromRevision,
                    paragraph.IsMoveToRevision);
                Font(text, paragraph.ParagraphBreakFont);
                break;
            case Table table:
                Join(text, table.StyleName, table.Alignment, table.PreferredWidth);
                break;
            case Row row:
                Join(text, row.RowFormat.AllowBreakAcrossPages, row.RowFormat.HeadingFormat, row.RowFormat.Height, row.RowFormat.HeightRule);
                break;
            case Cell cell:
                Join(text, cell.CellFormat.HorizontalMerge, cell.CellFormat.VerticalMerge, cell.CellFormat.Shading.BackgroundPatternColor.ToArgb());
                break;
            case Shape shape:
                Join(text, shape.Width, shape.Height, shape.WrapType, shape.ShapeType, shape.HasImage && shape.ImageData.ImageBytes is { } image ? Hash(image) : shape.ImageData?.SourceFullName ?? "-",
                    shape.TextPath.Text, shape.TextPath.FontFamily, shape.FillColor.ToArgb(), shape.Fill.Opacity,
                    shape.ImageData?.Contrast ?? 0, shape.ImageData?.Brightness ?? 0);
                break;
            case FieldStart start:
                Join(text, start.FieldType);
                break;
            case BookmarkStart start:
                Join(text, start.Name);
                break;
            case BookmarkEnd end:
                Join(text, end.Name);
                break;
            case Comment comment:
                Join(text, comment.Author, comment.Initial);
                break;
            case CommentRangeStart start:
                Join(text, start.Id);
                break;
            case CommentRangeEnd end:
                Join(text, end.Id);
                break;
            case Footnote footnote:
                Join(text, footnote.FootnoteType);
                break;
            case StructuredDocumentTag tag:
                Join(text, tag.Tag, tag.Title, tag.SdtType);
                break;
            case Inline inline:
                Revisions(text, inline);
                break;
        }

        if (node is CompositeNode composite)
        {
            // Adjacent runs of one format read as one text, however the engine splits them,
            // as a replacement of a text by itself splits the run it lands in.
            string? format = null;
            var runText = new StringBuilder();
            foreach (Node child in composite.GetChildNodes(NodeType.Any, false))
            {
                if (evaluation && IsEvaluationMark(child))
                {
                    continue;
                }

                if (child is Run run)
                {
                    string key = RunFormat(run);
                    if (format is not null && key != format)
                    {
                        AppendRun(text, runText, format);
                    }

                    format = key;
                    runText.Append(run.Text);
                    continue;
                }

                if (format is not null)
                {
                    AppendRun(text, runText, format);
                    format = null;
                }

                Append(text, child, evaluation);
            }

            if (format is not null)
            {
                AppendRun(text, runText, format);
            }
        }

        text.Append('>');
    }

    private static bool IsEvaluationMark(Node node) => node switch
    {
        Paragraph paragraph => WordsEvaluation.IsMark(paragraph),
        Shape shape => WordsEvaluation.IsWatermark(shape),
        _ => false,
    };

    private static void AppendRun(StringBuilder text, StringBuilder runText, string format)
    {
        text.Append('<').Append((int)NodeType.Run).Append('|').Append(runText).Append(format).Append('>');
        runText.Clear();
    }

    private static string RunFormat(Run run)
    {
        var format = new StringBuilder();
        Font(format, run.Font);
        Revisions(format, run);
        return format.ToString();
    }

    private static void Revisions(StringBuilder text, Inline inline) =>
        Join(text, inline.IsInsertRevision, inline.IsDeleteRevision, inline.IsFormatRevision, inline.IsMoveFromRevision, inline.IsMoveToRevision);

    private static void Font(StringBuilder text, Aspose.Words.Font font) =>
        Join(text, font.Name, font.NameAscii, font.NameFarEast, font.Size, font.Bold, font.Italic, font.Underline, font.Color.ToArgb(),
            font.HighlightColor.ToArgb(), font.StrikeThrough, font.StyleName);

    private static void Paragraph(StringBuilder text, ParagraphFormat format) =>
        Join(text, format.StyleName, format.Alignment, format.LeftIndent, format.RightIndent, format.FirstLineIndent, format.SpaceBefore,
            format.SpaceAfter, format.LineSpacing, format.LineSpacingRule, format.KeepWithNext, format.KeepTogether, format.PageBreakBefore,
            format.WidowControl, format.OutlineLevel);

    private static void Join(StringBuilder text, params object?[] values)
    {
        foreach (object? value in values)
        {
            text.Append('|').Append(value switch
            {
                null => "~",
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString(),
            });
        }
    }
}
