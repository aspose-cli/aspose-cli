using System.Globalization;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Words;

namespace Aspose.Cli.Product.Words.Engine.Editing;

/// <summary>
/// The document a <c>words edit</c> batch changes, as the SDK runner applies operations to it and
/// snapshots them (<see cref="WordsSnapshots"/>). Every address was resolved against the document
/// as it was before the first operation (<see cref="WordsAnchorResolver"/>), so an operation
/// changes what the caller read, whatever earlier operations inserted; an operation whose target
/// an earlier operation removed fails when the batch reaches it.
/// </summary>
internal sealed class WordsEditModel : IOperationModel<WordsOp>
{
    private readonly LoadedDocument _loaded;
    private readonly Dictionary<WordsOp, ResolvedWordsOp> _resolved = new(ReferenceEqualityComparer.Instance);
    private readonly Func<ResolvedWordsOp, WordsMutationHandlers> _handlers;
    private readonly WordsSnapshots _snapshots;
    private readonly Dictionary<WordsOp, long> _matched = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<WordsOp, string[]> _authors = new(ReferenceEqualityComparer.Instance);

    // Whether an operation other than a revision decision applied: numbered decisions name the
    // changes the input lists, which an edit can split, so they come first.
    private bool _edited;

    /// <param name="loaded">The document being edited.</param>
    /// <param name="resolved">The batch's operations with the addresses they resolved before it started.</param>
    /// <param name="handlers">Creates the handlers that apply one resolved operation.</param>
    internal WordsEditModel(LoadedDocument loaded, IReadOnlyList<ResolvedWordsOp> resolved, Func<ResolvedWordsOp, WordsMutationHandlers> handlers)
    {
        _loaded = loaded;
        _handlers = handlers;
        _snapshots = new WordsSnapshots(loaded);
        foreach (ResolvedWordsOp item in resolved)
        {
            _resolved[item.Op] = item;
        }
    }

    public OperationSnapshot Snapshot(WordsOp operation) => operation.Accept(_snapshots);

    /// <summary>The body blocks and the sections in order, as the nodes the snapshots add them as.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<object>> Positions() => new Dictionary<string, IReadOnlyList<object>>(StringComparer.Ordinal)
    {
        [WordsSnapshots.BlockUnit] = [.. WordsSnapshots.Blocks(_loaded)],
        [WordsSnapshots.SectionUnit] = [.. _loaded.Document.Sections.Cast<Section>()],
    };

    public void Apply(WordsOp operation)
    {
        ResolvedWordsOp resolved = _resolved[operation];
        if (resolved.Failure is { } failure)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }

        if (operation is RevisionDecisionOp { Revisions: not null } && _edited)
        {
            throw new OperationInvalidException(
                "with revisions it must come before every operation other than a revision decision",
                "Put the revision decisions first in the batch, or run them in a batch of their own; their numbers always name the changes the input lists.");
        }

        // The authors a review operation could match, as the document had them before it.
        _authors[operation] = operation switch
        {
            RevisionDecisionOp => [.. _loaded.Document.Revisions.Select(static revision => revision.Author)],
            DeleteCommentsOp => [.. _loaded.Document.GetChildNodes(NodeType.Comment, true).Cast<Comment>().Select(static comment => comment.Author)],
            _ => [],
        };
        _snapshots.Invalidate();
        WordsMutationHandlers handlers = _handlers(resolved);
        handlers.Run();
        _matched[operation] = handlers.Matched;
        _edited |= operation is not RevisionDecisionOp;
    }

    /// <summary>
    /// What a failed operation addressed: the blocks and sections it resolved before the batch;
    /// for one whose address did not resolve, the block or section numbers it names as written,
    /// which need not exist; otherwise the part of the document it changes. A heading, find text
    /// or bookmark that matched no block names no block, so it attempted <c>block/all</c>, the
    /// blocks it searched.
    /// </summary>
    public IReadOnlyList<string> AttemptedTargets(WordsOp operation)
    {
        ResolvedWordsOp resolved = _resolved[operation];
        string[] blocks = [.. resolved.Targets.Where(static target => target.StartsWith(WordsSnapshots.BlockUnit + "/", StringComparison.Ordinal))];
        string[] sections =
        [
            .. resolved.Sections.Select(section => _loaded.Document.Sections.IndexOf(section))
                .Where(static index => index >= 0)
                .Select(static index => $"{WordsSnapshots.SectionUnit}/{index + 1}"),
        ];
        if (blocks.Length + sections.Length > 0)
        {
            return [.. blocks, .. sections];
        }

        if (Named(operation) is { } named)
        {
            return [named];
        }

        return operation switch
        {
            SetPropertiesOp => ["property/all"],
            DefineStyleOp define => [WordsReadBack.StyleAddress(define.Name)],
            SetDefaultFontOp => ["style/all"],
            SetWatermarkOp or DeleteWatermarkOp => [WordsSnapshots.WatermarkAddress],
            ProtectDocumentOp or UnprotectDocumentOp => [WordsSnapshots.ProtectionAddress],
            SetPageSetupOp or SetHeaderOp or SetFooterOp or SetPageNumbersOp or InsertSectionOp or DeleteSectionOp => ["section/all"],
            _ => ["block/all"],
        };
    }

    /// <summary>The block number or range, or the section number, an operation names, or null when it names none.</summary>
    private static string? Named(WordsOp operation)
    {
        if (WordsAnchorResolver.TargetOf(operation) is { } target)
        {
            return target switch
            {
                { Block: int block } => string.Create(CultureInfo.InvariantCulture, $"{WordsSnapshots.BlockUnit}/{block}"),
                { Blocks: { } range } => $"{WordsSnapshots.BlockUnit}/{range}",
                _ => null,
            };
        }

        int? section = operation switch
        {
            InsertSectionOp insert => insert.At,
            DeleteSectionOp delete => delete.Section,
            SetPageSetupOp setup => setup.Section,
            HeaderFooterOp part => part.Section,
            SetPageNumbersOp numbers => numbers.Section,
            _ => null,
        };
        return section is int number ? string.Create(CultureInfo.InvariantCulture, $"{WordsSnapshots.SectionUnit}/{number}") : null;
    }

    public NoEffect? Explain(WordsOp operation) => operation switch
    {
        // Like a verification issue, it never repeats the find text.
        ReplaceTextOp replace when _matched.GetValueOrDefault(operation) == 0 => new NoEffect(NoEffectReasons.NoMatch,
            $"Operation {operation.Id} (replace_text) matched no text in scope '{replace.Scope}', so nothing was replaced.")
        {
            Hint = "Search with 'words search' and the same pattern and scope, or --scope all for headers, footers, "
                + "footnotes and comments. Field codes and text a tracked change deletes are never matched.",
        },
        RevisionDecisionOp { Author: { } author } => AuthorNoMatch(operation, author, "tracked revision", _authors.GetValueOrDefault(operation) ?? []),
        DeleteCommentsOp { Author: { } author } => AuthorNoMatch(operation, author, "comment", _authors.GetValueOrDefault(operation) ?? []),
        DeleteWatermarkOp => new NoEffect(NoEffectReasons.NoChange,
            $"Operation {operation.Id} (delete_watermark) changed nothing: the document has no watermark."),
        UnprotectDocumentOp => new NoEffect(NoEffectReasons.NoChange,
            $"Operation {operation.Id} (unprotect_document) changed nothing: the document has no editing restriction."),
        _ => null,
    };

    /// <summary>
    /// Why a review operation changed nothing when its author has no <paramref name="items"/> in
    /// the document: it names the closest of the authors it has.
    /// </summary>
    private static NoEffect? AuthorNoMatch(WordsOp operation, string author, string items, IReadOnlyList<string> present)
    {
        string[] authors = [.. present.Distinct(StringComparer.Ordinal)];
        if (authors.Contains(author, StringComparer.Ordinal))
        {
            return null;
        }

        string listed = string.Join(", ", authors.Select(static name => $"'{name}'"));
        return new NoEffect(NoEffectReasons.NoMatch,
            $"Operation {operation.Id} ({WordsOp.Catalog.NameOf(operation)}) changed nothing: the document has no {items} by '{author}'.")
        {
            Hint = authors.Length == 0
                ? $"The document has no {items}s; check 'words inspect --detail revisions comments'."
                : Mistake.Of(author, authors).Hint($"Authors match exactly; the document's are {listed}."),
        };
    }
}
