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
        var fields = new Stack<bool>();
        int codes = 0;
        Append(node, isRoot: true);
        return Clean(text.ToString());

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
                        text.Append(run.Text);
                    }

                    return;
                case CompositeNode composite:
                    for (Node? child = composite.FirstChild; child is not null; child = child.NextSibling)
                    {
                        Append(child, isRoot: false);
                    }

                    if (current is Paragraph)
                    {
                        text.Append(ControlChar.ParagraphBreakChar);
                    }

                    return;
            }
        }
    }

    /// <summary>Drops the trailing paragraph, cell and page marks and the cell marks inside raw SDK text.</summary>
    internal static string Clean(string value) =>
        value.TrimEnd('\r', '\a', '\f').Replace("\a", string.Empty, StringComparison.Ordinal);
}
