using Aspose.Words;
using Aspose.Words.Tables;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

internal sealed class DocumentBlockIndex
{
    private readonly IReadOnlyList<BlockEntry> _entries;
    private readonly Dictionary<Node, BlockEntry> _byNode;

    public DocumentBlockIndex(Document document)
    {
        var entries = new List<BlockEntry>();
        for (int sectionIndex = 0; sectionIndex < document.Sections.Count; sectionIndex++)
        {
            Section section = document.Sections[sectionIndex];
            foreach (Node node in section.Body.GetChildNodes(NodeType.Any, false))
            {
                if (node is Paragraph paragraph && IsEvaluationBanner(paragraph, entries.Count))
                {
                    continue;
                }

                if (node is Paragraph or Table)
                {
                    entries.Add(new BlockEntry(entries.Count + 1, sectionIndex + 1, node));
                }
            }
        }

        _entries = entries;
        _byNode = entries.ToDictionary(static entry => entry.Node);
    }

    public IReadOnlyList<BlockEntry> Entries => _entries;
    public int Count => _entries.Count;

    public BlockEntry Get(int index)
    {
        if (index < 1 || index > _entries.Count)
        {
            throw WordsErrors.BlockNotFound(index, _entries.Count);
        }

        return _entries[index - 1];
    }

    public int? FindBlock(Node node)
    {
        Node? current = node;
        while (current is not null)
        {
            if (_byNode.TryGetValue(current, out BlockEntry? entry))
            {
                return entry.Index;
            }

            current = current.ParentNode;
        }

        return null;
    }

    private static bool IsEvaluationBanner(Paragraph paragraph, int indexedBlockCount)
    {
        if (indexedBlockCount != 0)
        {
            return false;
        }

        string text = paragraph.GetText().Trim();
        return text.StartsWith(
                "Created with an evaluation copy of Aspose.Words.",
                StringComparison.Ordinal)
            && text.Contains(
                "https://products.aspose.com/words/temporary-license/",
                StringComparison.Ordinal);
    }
}

internal sealed record BlockEntry(int Index, int Section, Node Node);
