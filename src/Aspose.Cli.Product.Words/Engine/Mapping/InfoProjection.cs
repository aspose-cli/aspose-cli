using System.Text;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Text;
using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Fields;
using Aspose.Words.Tables;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

internal static class InfoProjection
{
    // The most entries a detail list returns; a longer list carries a LIST_TRUNCATED warning.
    private const int ListLimit = 1000;

    // The most characters of a revision's text; a longer text ends with an ellipsis.
    private const int RevisionTextLimit = 300;

    public static WordsInspectResult Project(LoadedDocument loaded, string path, WordsInspectRequest request)
    {
        Document document = loaded.Document;
        var index = new DocumentBlockIndex(document, loaded.Evaluation);
        HashSet<string> details = request.Details?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        NodeCollection paragraphs = document.GetChildNodes(NodeType.Paragraph, true);
        NodeCollection tables = document.GetChildNodes(NodeType.Table, true);
        document.UpdateWordCount();
        var warnings = new List<Warning>();

        return new WordsInspectResult
        {
            Input = Source(path, loaded),
            Document = new WordsDocumentSummary
            {
                SectionCount = document.Sections.Count,
                BlockCount = index.Count,
                ParagraphCount = paragraphs.Count,
                TableCount = tables.Count,
                PageCount = document.PageCount,
                WordCount = document.BuiltInDocumentProperties.Words,
                HasRevisions = document.Revisions.Count > 0,
                RevisionCount = document.Revisions.Count,
                RevisionAuthors = document.Revisions.Cast<Revision>().Select(static r => r.Author)
                    .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                CommentCount = document.GetChildNodes(NodeType.Comment, true).Count,
                Protection = WordsProtection.ToMode(document.ProtectionType),
                Signed = loaded.Format.HasDigitalSignature,
                HasMacros = loaded.Format.HasMacros,
                Watermark = WordsReadBack.Watermark(document),
            },
            Sections = details.Contains(WordsInspectDetails.Sections) ? Sections(document, loaded.Evaluation) : null,
            Outline = details.Contains(WordsInspectDetails.Outline) ? Outline(index, warnings) : null,
            Styles = details.Contains(WordsInspectDetails.Styles) ? WordsReadBack.Styles(document) : null,
            Fields = details.Contains(WordsInspectDetails.Fields) ? Fields(document, index, warnings) : null,
            Bookmarks = details.Contains(WordsInspectDetails.Bookmarks) ? document.Range.Bookmarks.Cast<Bookmark>()
                .Select(static b => b.Name).Order(StringComparer.Ordinal).ToArray() : null,
            Comments = details.Contains(WordsInspectDetails.Comments) ? Comments(document, index, warnings) : null,
            Revisions = details.Contains(WordsInspectDetails.Revisions) ? Revisions(document, index, warnings) : null,
            Images = details.Contains(WordsInspectDetails.Images) ? Images(document, index, warnings) : null,
            Tables = details.Contains(WordsInspectDetails.Tables) ? Tables(index) : null,
            Properties = details.Contains(WordsInspectDetails.Properties) ? Properties(document) : null,
            Fonts = details.Contains(WordsInspectDetails.Fonts) ? WordsFonts.Used(document) : null,
            // Last, so it holds the caps the detail lists above disclosed.
            Warnings = warnings.Count == 0 ? null : warnings,
        };
    }

    public static InputInfo Source(string path, LoadedDocument loaded) =>
        FileObjects.Input(path, loaded.FormatId, loaded.Format.IsEncrypted);

    private static IReadOnlyList<WordsSectionData> Sections(Document document, bool evaluation) =>
        document.Sections.Cast<Section>().Select((section, index) => new WordsSectionData
        {
            Section = index + 1,
            Orientation = section.PageSetup.Orientation == Orientation.Landscape ? WordsPageOrientations.Landscape : WordsPageOrientations.Portrait,
            PageSize = PageSizeInfo.Of(section.PageSetup.PageWidth, section.PageSetup.PageHeight),
            ColumnCount = section.PageSetup.TextColumns.Count,
            PageNumbers = WordsReadBack.PageNumbers(section),
            Margins = new WordsMarginData
            {
                Top = section.PageSetup.TopMargin,
                Right = section.PageSetup.RightMargin,
                Bottom = section.PageSetup.BottomMargin,
                Left = section.PageSetup.LeftMargin,
            },
            HeadersFooters = section.HeadersFooters.Cast<HeaderFooter>().Select(headerFooter =>
            {
                (string location, string kind) = WordsStories.PlaceOf(headerFooter);
                return new WordsHeaderFooterData
                {
                    Location = location,
                    Kind = kind,
                    // Evaluation mode writes its sentence into the footers of the documents it opens.
                    Paragraphs = WordsStories.Units(headerFooter).Cast<Paragraph>()
                        .Where(paragraph => !(evaluation && WordsEvaluation.IsMark(paragraph)))
                        .Select(static paragraph => new WordsParagraphData
                        {
                            Text = WordsText.Of(paragraph),
                            Style = paragraph.ParagraphFormat.StyleName,
                            List = WordsReadBack.List(paragraph),
                        })
                        .ToArray(),
                };
            }).ToArray(),
        }).ToArray();

    private static IReadOnlyList<WordsOutlineItem> Outline(DocumentBlockIndex index, List<Warning> warnings) =>
        Capped(
            index.Entries.Where(static entry => entry.Node is Paragraph p && HeadingLevel(p) is not null).ToArray(),
            static entry =>
            {
                var paragraph = (Paragraph)entry.Node;
                return new WordsOutlineItem
                {
                    Block = entry.Index,
                    HeadingLevel = HeadingLevel(paragraph)!.Value,
                    Text = WordsText.Of(paragraph),
                };
            },
            "outline",
            "Read every heading in windows with 'words read --content outline'.",
            warnings);

    private static IReadOnlyList<WordsFieldData> Fields(Document document, DocumentBlockIndex index, List<Warning> warnings) =>
        Capped(
            document.Range.Fields.Cast<Field>().ToArray(),
            field => new WordsFieldData
            {
                Kind = field.Type.ToString(),
                Scope = WordsStories.StoryOf(field.Start),
                Block = index.FindBlock(field.Start),
                Code = field.GetFieldCode(),
                Result = WordsText.ResultOf(field),
                MaxLevel = field is Aspose.Words.Fields.FieldToc toc ? TocMaxLevel(toc.HeadingLevelRange) : null,
            },
            "fields",
            "Split the document with 'words split --by sections' and inspect each part with '--detail fields'.",
            warnings);

    private static IReadOnlyList<WordsCommentData> Comments(Document document, DocumentBlockIndex index, List<Warning> warnings) =>
        Capped(
            document.GetChildNodes(NodeType.Comment, true).Cast<Comment>().ToArray(),
            comment => new WordsCommentData
            {
                Author = comment.Author,
                Text = WordsText.Of(comment),
                Block = index.FindBlock(comment),
            },
            "comments",
            "Extract every comment with 'words extract --what comments'.",
            warnings);

    /// <summary>
    /// One entry per logical change in document order. The SDK splits a change into one
    /// revision per run and paragraph mark and joins adjacent revisions of one type and author
    /// into a <see cref="RevisionGroup"/>, so a grouped change is listed once, at its first
    /// revision, with the group's text. <see cref="RevisionCollection.Groups"/> itself is not in
    /// document order, and style definition changes, moves and the revisions inside comments
    /// belong to no group. A style
    /// definition change is listed on its own; a move is joined by <see cref="Moves"/>. A
    /// deletion followed by an insertion stays two entries.
    /// </summary>
    private static IReadOnlyList<WordsRevisionData> Revisions(Document document, DocumentBlockIndex index, List<Warning> warnings)
    {
        (RevisionChange Change, int Number)[] changes = [.. RevisionChanges(document).Select(static (change, at) => (change, at + 1))];
        return Capped(
            changes,
            change => Entry(change.Change.First, change.Number, change.Change.Text, index),
            "revisions",
            "Split the document with 'words split --by sections' and inspect each part with '--detail revisions'.",
            warnings);
    }

    /// <summary>
    /// The logical changes of <paramref name="document"/> in document order, as inspect lists and
    /// numbers them (see <see cref="Revisions"/>), each with every revision it consists of.
    /// </summary>
    internal static IReadOnlyList<RevisionChange> RevisionChanges(Document document)
    {
        Revision[] revisions = document.Revisions.Cast<Revision>().ToArray();
        Dictionary<Revision, RevisionChange> moves = Moves(document, revisions);
        var groups = new Dictionary<RevisionGroup, List<Revision>>(ReferenceEqualityComparer.Instance);
        var changes = new List<RevisionChange>();
        foreach (Revision revision in revisions)
        {
            if (revision.RevisionType == RevisionType.Moving)
            {
                if (moves.TryGetValue(revision, out RevisionChange? move))
                {
                    changes.Add(move);
                }

                continue;
            }

            if (revision.Group is { } group && groups.TryGetValue(group, out List<Revision>? grouped))
            {
                grouped.Add(revision);
                continue;
            }

            List<Revision> members = [revision];
            if (revision.Group is { } first)
            {
                groups[first] = members;
            }

            changes.Add(new RevisionChange(revision, revision.RevisionType switch
            {
                RevisionType.Insertion or RevisionType.Deletion => revision.Group?.Text ?? NodeText(revision),
                // Format changes carry a description of the formatting, not document text.
                _ => null,
            }, members));
        }

        return changes;
    }

    /// <summary>
    /// The text of the one node a revision changes, or null for a paragraph: a paragraph's own
    /// revision is its mark alone, which has no text, while the SDK would return the whole
    /// paragraph's text.
    /// </summary>
    internal static string? NodeText(Revision revision) =>
        revision.ParentNode is Paragraph or null ? null : revision.ParentNode.GetText();

    /// <summary>
    /// Joins the move revisions into one move per side: its source (moved from) and its
    /// destination (moved to). The SDK records a move as one revision per inline node and per
    /// moved paragraph mark, in no <see cref="RevisionGroup"/>, and lists a paragraph mark before
    /// its paragraph's runs. Walking the paragraphs, each inline node and then the paragraph's
    /// mark, the nodes that follow one another with one direction, author and date are one move.
    /// Returns each move by the first of its revisions in <paramref name="revisions"/> order,
    /// with the move's text: its runs' text, with a paragraph break for each moved mark.
    /// </summary>
    private static Dictionary<Revision, RevisionChange> Moves(Document document, Revision[] revisions)
    {
        var moves = new Dictionary<Revision, RevisionChange>(ReferenceEqualityComparer.Instance);
        var byNode = new Dictionary<Node, Revision>(ReferenceEqualityComparer.Instance);
        var order = new Dictionary<Revision, int>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < revisions.Length; i++)
        {
            if (revisions[i].RevisionType == RevisionType.Moving)
            {
                byNode[revisions[i].ParentNode] = revisions[i];
                order[revisions[i]] = i;
            }
        }

        if (byNode.Count == 0)
        {
            return moves;
        }

        var walked = new HashSet<Revision>(ReferenceEqualityComparer.Instance);
        Revision? first = null;
        Revision? last = null;
        var members = new List<Revision>();
        var text = new StringBuilder();
        foreach (Node node in MoveOrder(document))
        {
            bool continues = byNode.TryGetValue(node, out Revision? revision) && last is not null
                && MovedFrom(last.ParentNode) == MovedFrom(node)
                && string.Equals(last.Author, revision!.Author, StringComparison.Ordinal)
                && last.DateTime == revision.DateTime;
            if (!continues && first is not null)
            {
                moves[first] = new RevisionChange(first, text.ToString(), members);
                first = last = null;
                members = [];
                text.Clear();
            }

            if (revision is null)
            {
                continue;
            }

            walked.Add(revision);
            members.Add(revision);
            if (first is null || order[revision] < order[first])
            {
                first = revision;
            }

            last = revision;
            // The text is cut to the limit when listed, so collecting stops just past it.
            if (text.Length <= RevisionTextLimit)
            {
                text.Append(node is Run run ? run.Text : node is Paragraph ? "\r" : string.Empty);
            }
        }

        if (first is not null)
        {
            moves[first] = new RevisionChange(first, text.ToString(), members);
        }

        // A move revision on a node the walk does not reach, such as a table row, is listed on its own.
        foreach (Revision revision in byNode.Values.Where(revision => !walked.Contains(revision)))
        {
            moves[revision] = new RevisionChange(revision, revision.ParentNode.GetText(), [revision]);
        }

        return moves;
    }

    // Each paragraph's own inline nodes, then the paragraph itself, standing for its mark.
    private static IEnumerable<Node> MoveOrder(Document document)
    {
        foreach (Paragraph paragraph in document.GetChildNodes(NodeType.Paragraph, true).Cast<Paragraph>())
        {
            foreach (Node node in paragraph.GetChildNodes(NodeType.Any, true))
            {
                if (node is Inline && node.GetAncestor(NodeType.Paragraph) == paragraph)
                {
                    yield return node;
                }
            }

            yield return paragraph;
        }
    }

    private static bool? MovedFrom(Node node) => node switch
    {
        Inline inline => inline.IsMoveFromRevision,
        Paragraph paragraph => paragraph.IsMoveFromRevision,
        _ => null,
    };

    private static WordsRevisionData Entry(Revision revision, int number, string? text, DocumentBlockIndex index)
    {
        bool style = revision.RevisionType == RevisionType.StyleDefinitionChange;
        text = text is null ? null : TextSearch.Truncate(WordsText.Clean(text), RevisionTextLimit);
        return new WordsRevisionData
        {
            Revision = number,
            Kind = RevisionKindName(revision.RevisionType),
            Author = revision.Author,
            // The SDK reports a revision without a recorded date as DateTime.MinValue. The date the
            // file declares is reported as UTC, as this CLI records it; Word records the author's
            // local time there instead, which the contract states.
            Date = revision.DateTime == DateTime.MinValue
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(revision.DateTime, DateTimeKind.Utc)),
            Scope = style ? null : WordsStories.ScopeOf(revision.ParentNode),
            Block = style ? null : index.FindBlock(revision.ParentNode),
            Text = string.IsNullOrEmpty(text) ? null : text,
        };
    }

    internal static string RevisionKindName(RevisionType type) => type switch
    {
        RevisionType.Insertion => WordsRevisionKinds.Insertion,
        RevisionType.Deletion => WordsRevisionKinds.Deletion,
        RevisionType.FormatChange => WordsRevisionKinds.FormatChange,
        RevisionType.StyleDefinitionChange => WordsRevisionKinds.StyleDefinitionChange,
        RevisionType.Moving => WordsRevisionKinds.Moving,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unmapped revision type."),
    };

    private static IReadOnlyList<WordsImageData> Images(Document document, DocumentBlockIndex index, List<Warning> warnings) =>
        Capped(
            document.GetChildNodes(NodeType.Shape, true).Cast<Shape>().Where(static shape => shape.HasImage).ToArray(),
            shape => new WordsImageData
            {
                Scope = WordsStories.StoryOf(shape),
                Block = index.FindBlock(shape),
                Name = shape.Name,
                Width = shape.Width,
                Height = shape.Height,
            },
            "images",
            "Extract every image with 'words extract --what images'.",
            warnings);

    /// <summary>
    /// Projects the first <see cref="ListLimit"/> entries of a detail list and discloses the
    /// cap with a <c>LIST_TRUNCATED</c> warning when the document holds more.
    /// </summary>
    private static IReadOnlyList<TItem> Capped<TSource, TItem>(
        IReadOnlyList<TSource> source,
        Func<TSource, TItem> project,
        string list,
        string hint,
        List<Warning> warnings)
    {
        if (source.Count > ListLimit)
        {
            warnings.Add(EnvelopeParts.ListTruncated(list, ListLimit, source.Count, hint));
        }

        return source.Take(ListLimit).Select(project).ToArray();
    }

    private static IReadOnlyList<WordsTableData> Tables(DocumentBlockIndex index) =>
        index.Entries.Where(static entry => entry.Node is Table).Select(static entry =>
        {
            var table = (Table)entry.Node;
            (bool keepTogether, bool allowRowBreak, int headerRows, bool keepWithNext) = WordsReadBack.Pagination(table);
            return new WordsTableData
            {
                KeepTogether = keepTogether,
                AllowRowBreakAcrossPages = allowRowBreak,
                HeaderRowCount = headerRows,
                KeepWithNext = keepWithNext,
                Block = entry.Index,
                RowCount = table.Rows.Count,
                ColumnCount = table.Rows.Count == 0 ? 0 : table.Rows.Cast<Row>().Max(static row => row.Cells.Count),
                TableStyle = string.IsNullOrEmpty(table.StyleName) ? "Table Normal" : table.StyleName,
            };
        }).ToArray();

    /// <summary>The built-in title, author, subject and keywords, and every custom property by its own name.</summary>
    private static IReadOnlyDictionary<string, string?> Properties(Document document)
    {
        var properties = new SortedDictionary<string, string?>(StringComparer.Ordinal)
        {
            ["title"] = document.BuiltInDocumentProperties.Title,
            ["author"] = document.BuiltInDocumentProperties.Author,
            ["subject"] = document.BuiltInDocumentProperties.Subject,
            ["keywords"] = document.BuiltInDocumentProperties.Keywords,
        };
        foreach (Aspose.Words.Properties.DocumentProperty property in document.CustomDocumentProperties)
        {
            properties[WordsReadBack.PropertyKey(property.Name)] = property.Value?.ToString();
        }

        return properties;
    }

    /// <summary>The last level of a table of contents' heading range, such as 3 of <c>1-3</c>.</summary>
    private static int? TocMaxLevel(string? range) =>
        range?.Split('-') is [_, var last] && int.TryParse(last, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int level)
            ? level
            : null;

    internal static int? HeadingLevel(Paragraph paragraph)
    {
        int level = (int)paragraph.ParagraphFormat.OutlineLevel;
        return level is >= 0 and <= 8 ? level + 1 : null;
    }
}

/// <summary>
/// One logical change as inspect lists it: the first of its revisions, the text it inserts,
/// deletes or moves, and every revision it consists of.
/// </summary>
internal sealed record RevisionChange(Revision First, string? Text, IReadOnlyList<Revision> Members)
{
    /// <summary>Matches the revisions of <paramref name="changes"/>.</summary>
    internal static IRevisionCriteria Criteria(IEnumerable<RevisionChange> changes) =>
        new MemberCriteria(changes.SelectMany(static change => change.Members).Select(RevisionKey.Of).ToHashSet(RevisionKey.Comparer));

    private sealed class MemberCriteria(HashSet<RevisionKey> keys) : IRevisionCriteria
    {
        public bool IsMatch(Revision revision) => keys.Contains(RevisionKey.Of(revision));
    }
}

/// <summary>
/// How a revision is known across reads. The SDK hands out new revision objects each time, so a
/// revision is known by the node or style it changes and its type; two revisions that share
/// both, such as a paragraph's format and its mark's character format, cannot be told apart.
/// </summary>
internal readonly record struct RevisionKey(object? Owner, RevisionType Type)
{
    internal static RevisionKey Of(Revision revision) =>
        new((object?)revision.ParentNode ?? revision.ParentStyle, revision.RevisionType);

    /// <summary>Compares owners by reference: nodes and styles have no value equality.</summary>
    internal static IEqualityComparer<RevisionKey> Comparer { get; } = new ReferenceComparer();

    private sealed class ReferenceComparer : IEqualityComparer<RevisionKey>
    {
        public bool Equals(RevisionKey left, RevisionKey right) =>
            left.Type == right.Type && ReferenceEquals(left.Owner, right.Owner);

        public int GetHashCode(RevisionKey key) =>
            HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(key.Owner), key.Type);
    }
}
