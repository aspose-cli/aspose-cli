using System.Globalization;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Text;
using Aspose.Words;

namespace Aspose.Cli.Product.Words.Engine.Editing;

/// <summary>
/// Resolves every public address against the original document before any
/// operation mutates the DOM. The returned nodes retain identity even when
/// insertions change the visible block numbering.
/// </summary>
internal static class WordsAnchorResolver
{
    public static IReadOnlyList<ResolvedWordsOp> Resolve(Document document, DocumentBlockIndex index, WordsOpsBatch batch)
    {
        var resolved = new List<ResolvedWordsOp>(batch.Ops.Count);
        foreach (WordsOp op in batch.Ops)
        {
            WordsTarget? target = TargetOf(op);
            IReadOnlyList<Node> nodes = target is null
                ? []
                : ResolveTarget(document, index, target);
            resolved.Add(new ResolvedWordsOp(op, nodes, ResolveSections(document, op), Targets(index, nodes, target)));
        }

        ValidateDeleteConflicts(resolved);
        return resolved;
    }

    private static IReadOnlyList<Section> ResolveSections(Document document, WordsOp op) => op switch
    {
        AddSectionOp { Position: "after", After: int after } => [WordsSections.Get(document, after)],
        DeleteSectionOp value => [WordsSections.Get(document, value.Section)],
        SetPageSetupOp value => SelectSections(document, value.Section),
        SetHeaderOp value => SelectSections(document, value.Section),
        SetFooterOp value => SelectSections(document, value.Section),
        SetPageNumbersOp value => SelectSections(document, value.Section),
        _ => [],
    };

    private static IReadOnlyList<Section> SelectSections(Document document, int? section) =>
        section is int number ? [WordsSections.Get(document, number)] : document.Sections.Cast<Section>().ToArray();

    internal static void EnsureAttached(Document document, ResolvedWordsOp operation)
    {
        if (operation.Nodes.Any(node => !ReferenceEquals(node.GetAncestor(NodeType.Document), document))
            || operation.Sections.Any(section => !ReferenceEquals(section.ParentNode, document)))
        {
            throw Invalid($"operation '{WordsOp.Catalog.NameOf(operation.Op)}' references an original object removed by an earlier operation");
        }
    }

    private static IReadOnlyList<Node> ResolveTarget(Document document, DocumentBlockIndex index, WordsTarget target)
    {
        if (target.Block is int block)
        {
            return [index.Get(block).Node];
        }

        if (target.Blocks is not null)
        {
            return index.Select(PageRange.Parse(target.Blocks)).Select(static entry => entry.Node).ToArray();
        }

        if (target.Bookmark is not null)
        {
            Bookmark? bookmark = document.Range.Bookmarks[target.Bookmark];
            if (bookmark is null)
            {
                // Bookmarks whose names begin with '_' are hidden ones Word maintains itself,
                // such as table of contents targets; they resolve but are not offered.
                throw CliErrors.NotFound(
                    ErrorCodes.BookmarkNotFound,
                    "bookmark",
                    target.Bookmark,
                    document.Range.Bookmarks
                        .Select(static item => item.Name)
                        .Where(static name => !name.StartsWith('_'))
                        .ToArray());
            }

            BlockEntry? entry = index.Find(bookmark.BookmarkStart);
            if (entry is null)
            {
                throw new CliException(
                    ErrorCodes.OpsInvalid,
                    $"Bookmark '{target.Bookmark}' is not inside a body block, so it cannot address one.",
                    hint: "Target a bookmark in the document body, or address the block by number, heading or text.");
            }

            return [entry.Node];
        }

        string needle = (target.Heading ?? target.Find)!;
        BlockEntry[] candidates = target.Heading is null
            ? [.. index.Entries]
            : [.. index.Entries.Where(static entry => entry.Node is Paragraph paragraph
                && InfoProjection.HeadingLevel(paragraph) is not null)];
        BlockEntry[] matches = candidates
            .Where(entry => WordsText.Of(entry.Node).Contains(needle, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matches.Length == 0)
        {
            throw AnchorMissing(index, target.Heading is null ? "text" : "heading", needle, candidates);
        }

        int nth = target.Nth ?? 1;
        if (matches.Length < nth)
        {
            string subject = target.Heading is null ? $"'{needle}' occurrence" : $"heading '{needle}' occurrence";
            throw CliErrors.NotFoundAt(
                WordsDiagnostics.AnchorNotFound, subject, nth.ToString(CultureInfo.InvariantCulture), matches.Length);
        }

        return [matches[nth - 1].Node];
    }

    /// <summary>
    /// The error for a heading or find text no candidate block contains. A name close to it among
    /// the candidates is suggested as for any name; otherwise the hint names the block, heading
    /// or not, that contains the longest leading part of the text, at least half of it, as a
    /// clause title remembered as "第六条 合同解除" leads to "第六条 合同的解除和终止".
    /// </summary>
    private static CliException AnchorMissing(DocumentBlockIndex index, string subject, string needle, BlockEntry[] candidates)
    {
        // Each block's text as far as it can name the block; a paragraph can be long.
        string[] available = [.. candidates
            .Select(static entry => WordsEngineSupport.Truncate(WordsText.Of(entry.Node).Trim(), 80))
            .Where(static text => text.Length > 0)];
        string? hint = null;
        if (NameSuggestions.Closest(needle, available).Count == 0 && Closest(index, needle) is { } closest)
        {
            string text = WordsText.Of(closest.Node).Trim();
            string heading = subject == "heading" && !candidates.Contains(closest) ? ", but it is not a heading" : string.Empty;
            hint = $"Block {closest.Index} holds the closest text: '{WordsEngineSupport.Truncate(text, 80)}'{heading}; "
                + $"address it with {{\"block\": {closest.Index}}} or a \"find\" text it contains.";
        }

        return CliErrors.NotFound(WordsDiagnostics.AnchorNotFound, subject, needle, available, hint);
    }

    // The first block that contains the longest leading part of the text, at least half of it.
    private static BlockEntry? Closest(DocumentBlockIndex index, string needle)
    {
        string[] texts = [.. index.Entries.Select(static entry => WordsText.Of(entry.Node))];
        for (int length = needle.Length - 1; length >= Math.Max(2, (needle.Length + 1) / 2); length--)
        {
            string part = needle[..length];
            int found = Array.FindIndex(texts, text => text.Contains(part, StringComparison.OrdinalIgnoreCase));
            if (found >= 0)
            {
                return index.Entries[found];
            }
        }

        return null;
    }

    /// <summary>
    /// The addresses of the original blocks that hold <paramref name="nodes"/>, followed by the
    /// headers and footers that hold them, as <c>section/{n}/{location}/{kind}</c>.
    /// </summary>
    internal static IReadOnlyList<string> Targets(
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
        string[] headersFooters = nodes
            .Select(static node => node.GetAncestor(NodeType.HeaderFooter))
            .OfType<HeaderFooter>()
            .Distinct()
            .Select(static headerFooter =>
            {
                (string location, string kind) = WordsStories.PlaceOf(headerFooter);
                return $"section/{WordsStories.SectionOf(headerFooter)}/{location}/{kind}";
            })
            .ToArray();
        if (blocks.Length > 100)
        {
            return target?.Blocks is { Length: > 0 } ranges ? [$"blocks/{ranges}", .. headersFooters] : ["document"];
        }

        string[] targets = [.. blocks.Select(static block => $"block/{block}"), .. headersFooters];
        return targets.Length == 0 ? ["document"] : targets;
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
        RepeatTableRowOp value => value.At,
        FormatTableOp value => value.At,
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
            foreach (Node target in item.Nodes.Concat<Node>(item.Sections))
            {
                for (Node? ancestor = target; ancestor is not null; ancestor = ancestor.ParentNode)
                {
                    if (deleted.Contains(ancestor))
                    {
                        throw Invalid($"op {index} ({WordsOp.Catalog.NameOf(item.Op)}) references an object deleted by an earlier op");
                    }
                }
            }

            if (item.Op is DeleteSectionOp)
            {
                Section section = item.Sections[0];
                if (deleted.Any(node => ReferenceEquals(node.GetAncestor(NodeType.Section), section)))
                {
                    throw Invalid($"op {index} (delete_section) overlaps an earlier block deletion");
                }
                deleted.Add(section);
            }
            else if (item.Op is DeleteBlocksOp)
            {
                deleted.UnionWith(item.Nodes);
            }
        }
    }

    private static CliException Invalid(string reason) => new(
        ErrorCodes.OpsInvalid,
        $"Invalid Words ops batch: {reason}.",
        hint: "Split conflicting deletes and dependent edits into separate batches.");
}

internal sealed record ResolvedWordsOp(
    WordsOp Op,
    IReadOnlyList<Node> Nodes,
    IReadOnlyList<Section> Sections,
    IReadOnlyList<string> Targets);
