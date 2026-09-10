using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Words;

namespace Aspose.Cli.Product.Words.Engine.Editing;

/// <summary>
/// Resolves every public address against the original document before any
/// operation mutates the DOM. The returned nodes retain identity even when
/// insertions change the visible block numbering.
/// </summary>
internal static class WordsAnchorResolver
{
    public static IReadOnlyList<ResolvedWordsOp> Resolve(Document document, WordsOpsBatch batch)
    {
        var index = new DocumentBlockIndex(document);
        var resolved = new List<ResolvedWordsOp>(batch.Ops.Count);
        foreach (WordsOp op in batch.Ops)
        {
            WordsTarget? target = TargetOf(op);
            IReadOnlyList<Node> nodes = target is null
                ? []
                : ResolveTarget(document, index, target);
            resolved.Add(new ResolvedWordsOp(op, nodes, Targets(index, nodes, target)));
        }

        ValidateDeleteConflicts(resolved);
        return resolved;
    }

    private static IReadOnlyList<Node> ResolveTarget(Document document, DocumentBlockIndex index, WordsTarget target)
    {
        if (target.Block is int block)
        {
            return [index.Get(block).Node];
        }

        if (target.Blocks is not null)
        {
            return PageRange.Parse(target.Blocks).Resolve(index.Count).Select(value => index.Get(value).Node).ToArray();
        }

        if (target.Bookmark is not null)
        {
            Bookmark? bookmark = document.Range.Bookmarks[target.Bookmark];
            if (bookmark is null)
            {
                throw new CliException(
                    WordsDiagnostics.BookmarkNotFound,
                    $"Bookmark '{target.Bookmark}' was not found.",
                    hint: "Run 'words inspect --detail bookmarks' and use an available bookmark.");
            }

            Node? node = TopLevelBlock(bookmark.BookmarkStart);
            if (node is null)
            {
                throw AnchorNotFound($"bookmark '{target.Bookmark}' is not inside a body block");
            }

            return [node];
        }

        string? needle = target.Heading ?? target.Find;
        IEnumerable<BlockEntry> candidates = index.Entries;
        if (target.Heading is not null)
        {
            candidates = candidates.Where(static entry => entry.Node is Paragraph paragraph
                && InfoProjection.HeadingLevel(paragraph) is not null);
        }

        BlockEntry[] matches = candidates
            .Where(entry => InfoProjection.Clean(entry.Node.GetText()).Contains(needle!, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        int nth = target.Nth ?? 1;
        if (matches.Length < nth)
        {
            throw AnchorNotFound($"'{needle}' occurrence {nth} was not found");
        }

        return [matches[nth - 1].Node];
    }

    private static IReadOnlyList<string> Targets(
        DocumentBlockIndex index,
        IReadOnlyList<Node> nodes,
        WordsTarget? target)
    {
        int[] blocks = nodes
            .Select(index.FindBlock)
            .OfType<int>()
            .Distinct()
            .Order()
            .ToArray();
        if (blocks.Length == 0)
        {
            return ["document"];
        }
        if (blocks.Length <= 100)
        {
            return blocks.Select(static block => $"block/{block}").ToArray();
        }
        return target?.Blocks is { Length: > 0 } ranges
            ? [$"blocks/{ranges}"]
            : ["document"];
    }

    private static Node? TopLevelBlock(Node node)
    {
        Node? current = node;
        while (current is not null)
        {
            if (current is Paragraph or Aspose.Words.Tables.Table
                && current.ParentNode is Body)
            {
                return current;
            }

            current = current.ParentNode;
        }

        return null;
    }

    private static WordsTarget? TargetOf(WordsOp op) => op switch
    {
        SetTextOp value => value.At,
        InsertParagraphsOp value => value.At,
        InsertMarkdownOp value => value.At,
        DeleteBlocksOp value => value.Target,
        InsertBreakOp value => value.At,
        InsertImageOp value => value.At,
        InsertTableOp value => value.At,
        SetTableCellOp value => value.At,
        InsertTocOp value => value.At,
        InsertBookmarkOp value => value.At,
        InsertHyperlinkOp value => value.At,
        InsertFieldOp value => value.At,
        FormatTextOp value => value.Target,
        SetStyleOp value => value.Target,
        ApplyListOp value => value.Target,
        AddCommentOp value => value.At,
        _ => null,
    };

    private static void ValidateDeleteConflicts(IReadOnlyList<ResolvedWordsOp> resolved)
    {
        var deleted = new HashSet<Node>();
        for (int index = 0; index < resolved.Count; index++)
        {
            ResolvedWordsOp item = resolved[index];
            foreach (Node node in item.Nodes)
            {
                if (deleted.Contains(node))
                {
                    throw Invalid($"op {index} ({item.Op.OpName}) references a block deleted by an earlier op");
                }
            }

            if (item.Op is not DeleteBlocksOp)
            {
                continue;
            }

            foreach (Node node in item.Nodes)
            {
                if (!deleted.Add(node))
                {
                    throw Invalid($"op {index} (delete_blocks) overlaps an earlier deletion");
                }
            }
        }
    }

    private static CliException AnchorNotFound(string reason) => new(
        WordsDiagnostics.AnchorNotFound,
        $"Document anchor not found: {reason}.",
        hint: "Inspect the document with 'words inspect' or 'words query blocks', then use a current block, bookmark or heading.");

    private static CliException Invalid(string reason) => new(
        ErrorCodes.OpsInvalid,
        $"Invalid Words ops batch: {reason}.",
        hint: "Split conflicting deletes and dependent edits into separate batches.");
}

internal sealed record ResolvedWordsOp(
    WordsOp Op,
    IReadOnlyList<Node> Nodes,
    IReadOnlyList<string> Targets);
