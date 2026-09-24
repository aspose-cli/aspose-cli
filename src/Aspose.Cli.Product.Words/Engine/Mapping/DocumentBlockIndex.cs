using Aspose.Words;
using Aspose.Words.Tables;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

internal sealed class DocumentBlockIndex
{
    private readonly IReadOnlyList<BlockEntry> _entries;
    private readonly Dictionary<Node, BlockEntry> _byNode;

    /// <summary>
    /// Indexes the top-level blocks. Under evaluation the banner paragraphs that evaluation
    /// mode inserts before the first block are not blocks.
    /// </summary>
    public DocumentBlockIndex(Document document, bool evaluation)
    {
        var entries = new List<BlockEntry>();
        HashSet<Node> banners = evaluation ? [.. WordsEvaluation.LeadingBanners(document)] : [];
        for (int sectionIndex = 0; sectionIndex < document.Sections.Count; sectionIndex++)
        {
            Section section = document.Sections[sectionIndex];
            foreach (Node node in section.Body.GetChildNodes(NodeType.Any, false))
            {
                if (node is Paragraph or Table && !banners.Contains(node))
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

    /// <summary>The blocks a 1-based range names, in order; a range past the last block is BLOCK_NOT_FOUND.</summary>
    public IReadOnlyList<BlockEntry> Select(PageRange range) =>
        range.Resolve(_entries.Count, WordsErrors.BlockRangeNotFound).Select(Get).ToArray();

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

}

internal sealed record BlockEntry(int Index, int Section, Node Node);
