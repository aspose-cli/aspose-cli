using System.Globalization;
using Aspose.Cli.Sdk.Errors;
using Aspose.Words;
using Aspose.Words.Markup;
using Aspose.Words.Tables;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

internal sealed class DocumentBlockIndex
{
    private readonly IReadOnlyList<BlockEntry> _entries;
    private readonly Dictionary<Node, BlockEntry> _byNode;

    /// <summary>
    /// Indexes the blocks of every section body. Under evaluation the banner paragraphs that
    /// evaluation mode inserts before the first block are not blocks. The list numbers the
    /// blocks' text reads are brought up to date.
    /// </summary>
    public DocumentBlockIndex(Document document, bool evaluation)
    {
        document.UpdateListLabels();
        var entries = new List<BlockEntry>();
        HashSet<Node> banners = evaluation ? [.. WordsEvaluation.LeadingBanners(document)] : [];
        for (int sectionIndex = 0; sectionIndex < document.Sections.Count; sectionIndex++)
        {
            foreach (Node node in BodyBlocks(document.Sections[sectionIndex].Body))
            {
                if (!banners.Contains(node))
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

    /// <summary>
    /// The paragraphs and tables of a body in document order. A block-level content control
    /// is a container, not a block: its paragraphs and tables, including those of nested
    /// controls, are blocks in their own right, so a table of contents or a form region
    /// wrapped in a control is addressable.
    /// </summary>
    public static IReadOnlyList<Node> BodyBlocks(Body body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var blocks = new List<Node>();
        Collect(body);
        return blocks;

        void Collect(CompositeNode container)
        {
            for (Node? child = container.FirstChild; child is not null; child = child.NextSibling)
            {
                if (child is Paragraph or Table)
                {
                    blocks.Add(child);
                }
                else if (child is StructuredDocumentTag control)
                {
                    Collect(control);
                }
            }
        }
    }

    /// <summary>Removes a block and every content control it leaves empty.</summary>
    public static void Remove(Node block)
    {
        ArgumentNullException.ThrowIfNull(block);
        CompositeNode? container = block.ParentNode;
        block.Remove();
        while (container is StructuredDocumentTag { HasChildNodes: false })
        {
            CompositeNode? parent = container.ParentNode;
            container.Remove();
            container = parent;
        }
    }

    public BlockEntry Get(int index)
    {
        if (index < 1 || index > _entries.Count)
        {
            throw CliErrors.NotFoundAt(
                WordsDiagnostics.BlockNotFound, "block", index.ToString(CultureInfo.InvariantCulture), _entries.Count);
        }

        return _entries[index - 1];
    }

    /// <summary>The blocks a 1-based range names, in order; a range past the last block is BLOCK_NOT_FOUND.</summary>
    public IReadOnlyList<BlockEntry> Select(PageRange range) =>
        range.Resolve(_entries.Count, WordsDiagnostics.BlockNotFound, "block").Select(Get).ToArray();

    /// <summary>The block that contains a node, or null outside every block.</summary>
    public BlockEntry? Find(Node node)
    {
        for (Node? current = node; current is not null; current = current.ParentNode)
        {
            if (_byNode.TryGetValue(current, out BlockEntry? entry))
            {
                return entry;
            }
        }

        return null;
    }

    public int? FindBlock(Node node) => Find(node)?.Index;
}

internal sealed record BlockEntry(int Index, int Section, Node Node);
