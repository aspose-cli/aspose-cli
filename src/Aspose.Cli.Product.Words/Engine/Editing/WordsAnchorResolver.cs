using System.Globalization;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Text;
using Aspose.Words;

namespace Aspose.Cli.Product.Words.Engine.Editing;

/// <summary>
/// Resolves every public address against the original document before any
/// operation mutates the DOM. The returned nodes retain identity even when
/// insertions change the visible block numbering. An operation whose address does not
/// resolve keeps its failure, which it raises when the batch reaches it, so the error names
/// the operation and a best-effort batch keeps the others.
/// </summary>
internal static class WordsAnchorResolver
{
    public static IReadOnlyList<ResolvedWordsOp> Resolve(Document document, DocumentBlockIndex index, WordsOpsBatch batch)
    {
        var resolved = new List<ResolvedWordsOp>(batch.Ops.Count);
        IReadOnlyList<RevisionChange>? changes = null;
        for (int position = 0; position < batch.Ops.Count; position++)
        {
            WordsOp op = batch.Ops[position];
            try
            {
                if (op is RevisionDecisionOp { Revisions: { } numbers })
                {
                    // The numbers name the changes the input lists; the model refuses a numbered
                    // decision after an edit that applied, which can split those changes.
                    changes ??= InfoProjection.RevisionChanges(document);
                    RevisionChange[] selected = SelectRevisions(changes, numbers);
                    Node[] changed = [.. selected.SelectMany(static change => change.Members)
                        .Select(static revision => revision.ParentNode).OfType<Node>().Distinct()];
                    resolved.Add(new ResolvedWordsOp(op, [], [], Targets(index, changed)) { Revisions = selected });
                    continue;
                }

                WordsTarget? target = TargetOf(op);
                IReadOnlyList<Node> nodes = target is null
                    ? []
                    : ResolveTarget(document, index, target);
                resolved.Add(new ResolvedWordsOp(op, nodes, ResolveSections(document, op), Targets(index, nodes)));
            }
            catch (Exception failure) when (failure is CliException or OperationInvalidException)
            {
                resolved.Add(new ResolvedWordsOp(op, [], [], []) { Failure = failure });
            }
        }

        return resolved;
    }

    // The changes inspect --detail revisions numbers, each once, in document order.
    private static RevisionChange[] SelectRevisions(IReadOnlyList<RevisionChange> changes, IReadOnlyList<int> numbers)
    {
        if (numbers.FirstOrDefault(number => number > changes.Count) is > 0 and int missing)
        {
            throw CliErrors.NotFoundAt(
                WordsDiagnostics.RevisionNotFound,
                "revision",
                missing.ToString(CultureInfo.InvariantCulture),
                changes.Count,
                hint: changes.Count == 0
                    ? "The document has no tracked revisions."
                    : $"Use a revision from 1 through {changes.Count}, as 'words inspect --detail revisions' numbers them.");
        }

        int[] wanted = [.. numbers.Distinct().Order()];

        // The engine knows a revision only by the node it changes and its type, so changes that
        // share both, such as a paragraph's format and its mark's character format, are decided
        // together.
        var owners = new Dictionary<RevisionKey, List<int>>(RevisionKey.Comparer);
        for (int number = 1; number <= changes.Count; number++)
        {
            foreach (RevisionKey key in changes[number - 1].Members.Select(RevisionKey.Of))
            {
                (owners.TryGetValue(key, out List<int>? listed) ? listed : owners[key] = []).Add(number);
            }
        }

        foreach (int number in wanted)
        {
            int[] together = [.. changes[number - 1].Members.Select(RevisionKey.Of).SelectMany(key => owners[key]).Distinct().Order()];
            if (together.Except(wanted).Any())
            {
                throw new OperationInvalidException(
                    $"revisions {string.Join(" and ", together)} change the same node in the same way and can only be decided together",
                    "List all of them in revisions, or none.");
            }
        }

        return [.. wanted.Select(number => changes[number - 1])];
    }

    private static IReadOnlyList<Section> ResolveSections(Document document, WordsOp op) => op switch
    {
        InsertSectionOp value => [WordsSections.Get(document, value.At)],
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
            throw new OperationInvalidException(
                "it references an object deleted by an earlier operation", DeleteConflictHint);
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
                throw new OperationInvalidException(
                    $"bookmark '{target.Bookmark}' is not inside a body block, so it cannot address one",
                    "Target a bookmark in the document body, or address the block by number, heading or text.");
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
            .Select(static entry => TextSearch.Truncate(WordsText.Of(entry.Node).Trim(), 80))
            .Where(static text => text.Length > 0)];
        string? hint = null;
        if (Mistake.Of(needle, available).Suggestions.Count == 0 && Closest(index, needle) is { } closest)
        {
            string text = WordsText.Of(closest.Node).Trim();
            string heading = subject == "heading" && !candidates.Contains(closest) ? ", but it is not a heading" : string.Empty;
            hint = $"Block {closest.Index} holds the closest text: '{TextSearch.Truncate(text, 80)}'{heading}; "
                + $"address it with {{\"block\": {closest.Index}}} or a \"find\" text it contains.";
        }

        return CliErrors.TextNotFound(WordsDiagnostics.AnchorNotFound, subject, needle, available, hint);
    }

    // The first block that contains the longest leading part of the text, at least half of it.
    // Spaces do not count, as a half-width space typed for a full-width one is a common slip,
    // and body blocks come before table of contents entries, which repeat the headings' text.
    private static BlockEntry? Closest(DocumentBlockIndex index, string needle)
    {
        string wanted = WithoutSpaces(needle);
        (BlockEntry Entry, string Text)[] blocks = [.. index.Entries
            .OrderBy(static entry => entry.Node is Paragraph
            {
                ParagraphFormat.StyleIdentifier: >= StyleIdentifier.Toc1 and <= StyleIdentifier.Toc9,
            })
            .Select(static entry => (entry, WithoutSpaces(WordsText.Of(entry.Node))))];
        for (int length = wanted.Length; length >= Math.Max(2, (wanted.Length + 1) / 2); length--)
        {
            string part = wanted[..length];
            foreach ((BlockEntry entry, string text) in blocks)
            {
                if (text.Contains(part, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }
        }

        return null;
    }

    private static string WithoutSpaces(string text) => string.Concat(text.Where(static c => !char.IsWhiteSpace(c)));

    /// <summary>The addresses of the original blocks that hold <paramref name="nodes"/>, as <c>block/{n}</c>.</summary>
    internal static IReadOnlyList<string> Targets(DocumentBlockIndex index, IReadOnlyList<Node> nodes) =>
        [.. nodes.Select(index.FindBlock).OfType<int>().Distinct().Order().Select(static block => $"block/{block}")];

    private static WordsTarget? TargetOf(WordsOp op) => op switch
    {
        SetTextOp value => value.Target,
        InsertParagraphsOp value => value.At,
        InsertMarkdownOp value => value.At,
        DeleteBlocksOp value => value.Target,
        InsertBreakOp value => value.At,
        InsertImageOp value => value.At,
        InsertTableOp value => value.At,
        SetTableCellOp value => value.Target,
        RepeatTableRowOp value => value.At,
        SetTablePaginationOp value => value.Target,
        InsertTocOp value => value.At,
        InsertBookmarkOp value => value.At,
        InsertHyperlinkOp value => value.At,
        InsertFieldOp value => value.At,
        FormatTextOp value => value.Target,
        SetStyleOp value => value.Target,
        SetListOp value => value.Target,
        AddCommentOp value => value.Target,
        _ => null,
    };

    private const string DeleteConflictHint = "Split conflicting deletes and dependent edits into separate batches.";
}

internal sealed record ResolvedWordsOp(
    WordsOp Op,
    IReadOnlyList<Node> Nodes,
    IReadOnlyList<Section> Sections,
    IReadOnlyList<string> Targets)
{
    /// <summary>The numbered changes a revision decision names, or null when it names none.</summary>
    public IReadOnlyList<RevisionChange>? Revisions { get; init; }

    /// <summary>
    /// Why the operation's address did not resolve, or null when it did: an
    /// <see cref="OperationInvalidException"/> or a domain <see cref="CliException"/>, raised
    /// when the batch reaches the operation.
    /// </summary>
    public Exception? Failure { get; init; }
}
