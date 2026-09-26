using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Results;
using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Fields;
using Aspose.Words.Tables;
using ContractFieldData = Aspose.Cli.Product.Words.Contracts.FieldData;
using ContractImageData = Aspose.Cli.Product.Words.Contracts.ImageData;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

internal static class InfoProjection
{
    // The most entries a detail list returns; a longer list carries a LIST_TRUNCATED warning.
    private const int ListLimit = 1000;

    public static DocumentInfoResult Project(LoadedDocument loaded, string path, DocumentInfoRequest request)
    {
        Document document = loaded.Document;
        var index = new DocumentBlockIndex(document, loaded.Evaluation);
        HashSet<string> details = request.Details?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        NodeCollection paragraphs = document.GetChildNodes(NodeType.Paragraph, true);
        NodeCollection tables = document.GetChildNodes(NodeType.Table, true);
        document.UpdateWordCount();
        var warnings = new List<Warning>();

        return new DocumentInfoResult
        {
            Source = Source(path, loaded),
            Document = new DocumentSummary
            {
                Sections = document.Sections.Count,
                Blocks = index.Count,
                Paragraphs = paragraphs.Count,
                Tables = tables.Count,
                Pages = document.PageCount,
                Words = document.BuiltInDocumentProperties.Words,
                RevisionsPresent = document.Revisions.Count > 0,
                RevisionCount = document.Revisions.Count,
                RevisionAuthors = document.Revisions.Cast<Revision>().Select(static r => r.Author)
                    .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                Protection = document.ProtectionType.ToString(),
                Signed = loaded.Format.HasDigitalSignature,
            },
            Sections = details.Contains("sections") ? Sections(document) : null,
            Outline = details.Contains("outline") || request.IncludePreview ? Outline(index, warnings) : null,
            Styles = details.Contains("styles") ? document.Styles.Cast<Style>()
                .Select(static s => s.Name).Order(StringComparer.Ordinal).ToArray() : null,
            Fields = details.Contains("fields") ? Fields(document, index, warnings) : null,
            Bookmarks = details.Contains("bookmarks") ? document.Range.Bookmarks.Cast<Bookmark>()
                .Select(static b => b.Name).Order(StringComparer.Ordinal).ToArray() : null,
            Comments = details.Contains("comments") ? Comments(document, index, warnings) : null,
            Images = details.Contains("images") ? Images(document, index, warnings) : null,
            Tables = details.Contains("tables") ? Tables(index) : null,
            Properties = details.Contains("properties") ? Properties(document) : null,
            Fonts = details.Contains("fonts") ? WordsFonts.Used(document) : null,
            // Last, so it holds the caps the detail lists above disclosed.
            Warnings = warnings.Count == 0 ? null : warnings,
        };
    }

    public static SourceInfo Source(string path, LoadedDocument loaded) => new()
    {
        Path = Path.GetFullPath(path),
        Format = loaded.FormatId,
        SizeBytes = new FileInfo(path).Length,
        Fingerprint = FileFingerprints.Capture(path),
    };

    private static IReadOnlyList<SectionData> Sections(Document document) =>
        document.Sections.Cast<Section>().Select((section, index) => new SectionData
        {
            Index = index + 1,
            Orientation = section.PageSetup.Orientation.ToString(),
            WidthPoints = section.PageSetup.PageWidth,
            HeightPoints = section.PageSetup.PageHeight,
            Margins = new MarginData
            {
                Top = section.PageSetup.TopMargin,
                Right = section.PageSetup.RightMargin,
                Bottom = section.PageSetup.BottomMargin,
                Left = section.PageSetup.LeftMargin,
            },
        }).ToArray();

    private static IReadOnlyList<OutlineItem> Outline(DocumentBlockIndex index, List<Warning> warnings) =>
        Capped(
            index.Entries.Where(static entry => entry.Node is Paragraph p && HeadingLevel(p) is not null).ToArray(),
            static entry =>
            {
                var paragraph = (Paragraph)entry.Node;
                return new OutlineItem
                {
                    Block = entry.Index,
                    Level = HeadingLevel(paragraph)!.Value,
                    Text = WordsText.Of(paragraph),
                };
            },
            "outline",
            "Read every heading in windows with 'words query blocks --scope outline'.",
            warnings);

    private static IReadOnlyList<ContractFieldData> Fields(Document document, DocumentBlockIndex index, List<Warning> warnings) =>
        Capped(
            document.Range.Fields.Cast<Field>().ToArray(),
            field => new ContractFieldData
            {
                Type = field.Type.ToString(),
                Block = index.FindBlock(field.Start) ?? 0,
                Code = field.GetFieldCode(),
                Result = field.Result,
            },
            "fields",
            "Split the document with 'words split --by section' and inspect each part with '--detail fields'.",
            warnings);

    private static IReadOnlyList<CommentData> Comments(Document document, DocumentBlockIndex index, List<Warning> warnings) =>
        Capped(
            document.GetChildNodes(NodeType.Comment, true).Cast<Comment>().ToArray(),
            comment => new CommentData
            {
                Author = comment.Author,
                Text = WordsText.Of(comment),
                Block = index.FindBlock(comment),
            },
            "comments",
            "Extract every comment with 'words extract --what comments'.",
            warnings);

    private static IReadOnlyList<ContractImageData> Images(Document document, DocumentBlockIndex index, List<Warning> warnings) =>
        Capped(
            document.GetChildNodes(NodeType.Shape, true).Cast<Shape>().Where(static shape => shape.HasImage).ToArray(),
            shape => new ContractImageData
            {
                Block = index.FindBlock(shape) ?? 0,
                Name = shape.Name,
                WidthPoints = shape.Width,
                HeightPoints = shape.Height,
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

    private static IReadOnlyList<TableData> Tables(DocumentBlockIndex index) =>
        index.Entries.Where(static entry => entry.Node is Table).Select(static entry =>
        {
            var table = (Table)entry.Node;
            return new TableData
            {
                Block = entry.Index,
                Rows = table.Rows.Count,
                Columns = table.Rows.Count == 0 ? 0 : table.Rows.Cast<Row>().Max(static row => row.Cells.Count),
            };
        }).ToArray();

    private static IReadOnlyDictionary<string, string?> Properties(Document document) =>
        new SortedDictionary<string, string?>(StringComparer.Ordinal)
        {
            ["title"] = document.BuiltInDocumentProperties.Title,
            ["author"] = document.BuiltInDocumentProperties.Author,
            ["subject"] = document.BuiltInDocumentProperties.Subject,
            ["keywords"] = document.BuiltInDocumentProperties.Keywords,
        };

    internal static int? HeadingLevel(Paragraph paragraph)
    {
        int level = (int)paragraph.ParagraphFormat.OutlineLevel;
        return level is >= 0 and <= 8 ? level + 1 : null;
    }
}
