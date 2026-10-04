using System.Text;
using Aspose.Words;
using Aspose.Words.Fields;
using Aspose.Words.Notes;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

/// <summary>
/// The one text projection of document content: the text a reader sees with tracked changes
/// applied. A field shows its result, never its code; text a revision deletes is gone; and a
/// comment or footnote belongs to its own story, not to the paragraph that anchors it. Blocks,
/// search snippets, text addresses, outlines and review labels all read text through here.
/// </summary>
internal static class WordsText
{
    /// <summary>The visible text of a node; nested paragraphs are separated by a paragraph mark.</summary>
    internal static string Of(Node node)
    {
        var text = new StringBuilder();
        Walk(
            node,
            run => text.Append(run.Text),
            paragraphStart: () =>
            {
                // A nested paragraph, such as a text box's, starts on a line of its own.
                if (text.Length > 0 && text[^1] != ControlChar.ParagraphBreakChar)
                {
                    text.Append(ControlChar.ParagraphBreakChar);
                }
            },
            paragraphEnd: () => text.Append(ControlChar.ParagraphBreakChar));
        return Clean(text.ToString());
    }

    /// <summary>
    /// The result of a field as a reader sees it: the results of the fields nested in it, such as
    /// a table of contents' hyperlinks and page references, without their codes and marks.
    /// </summary>
    internal static string ResultOf(Field field)
    {
        var text = new StringBuilder();
        var fields = new Stack<bool>();
        int codes = 0;
        foreach (char c in field.Result ?? string.Empty)
        {
            switch (c)
            {
                case ControlChar.FieldStartChar:
                    fields.Push(true);
                    codes++;
                    break;
                case ControlChar.FieldSeparatorChar when fields.TryPeek(out bool code) && code:
                    fields.Pop();
                    fields.Push(false);
                    codes--;
                    break;
                case ControlChar.FieldEndChar when fields.TryPop(out bool code):
                    codes -= code ? 1 : 0;
                    break;
                default:
                    if (codes == 0)
                    {
                        text.Append(c);
                    }

                    break;
            }
        }

        return Clean(text.ToString());
    }

    /// <summary>The runs whose text <see cref="Of"/> reads from a node, in order.</summary>
    internal static IReadOnlyList<Run> VisibleRuns(Node node)
    {
        var runs = new List<Run>();
        Walk(node, runs.Add, paragraphStart: null, paragraphEnd: null);
        return runs;
    }

    private static void Walk(Node node, Action<Run> visible, Action? paragraphStart, Action? paragraphEnd)
    {
        var fields = new Stack<bool>();
        int codes = 0;
        Append(node, isRoot: true);

        void Append(Node current, bool isRoot)
        {
            switch (current)
            {
                case Comment or Footnote when !isRoot:
                    return;
                case FieldStart:
                    fields.Push(true);
                    codes++;
                    return;
                case FieldSeparator when fields.TryPeek(out bool code) && code:
                    fields.Pop();
                    fields.Push(false);
                    codes--;
                    return;
                case FieldEnd when fields.TryPop(out bool code):
                    codes -= code ? 1 : 0;
                    return;
                case Run run:
                    if (codes == 0 && !run.IsDeleteRevision)
                    {
                        visible(run);
                    }

                    return;
                case CompositeNode composite:
                    if (current is Paragraph)
                    {
                        paragraphStart?.Invoke();
                    }

                    for (Node? child = composite.FirstChild; child is not null; child = child.NextSibling)
                    {
                        Append(child, isRoot: false);
                    }

                    if (current is Paragraph)
                    {
                        paragraphEnd?.Invoke();
                    }

                    return;
            }
        }
    }

    /// <summary>
    /// The visible text of blocks as plain text: one line per paragraph, including each
    /// paragraph of a table cell or text box, with manual line and page breaks as line ends.
    /// </summary>
    internal static string Lines(IEnumerable<Node> blocks) =>
        string.Join('\n', blocks.Select(static block => Of(block)
            .Replace(ControlChar.ParagraphBreakChar, '\n')
            .Replace(ControlChar.LineBreakChar, '\n')
            .Replace(ControlChar.PageBreakChar, '\n')));

    /// <summary>Drops the trailing paragraph, cell and page marks and the cell marks inside raw SDK text.</summary>
    internal static string Clean(string value) =>
        value.TrimEnd('\r', '\a', '\f').Replace("\a", string.Empty, StringComparison.Ordinal);
}
