using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO;
using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Fields;
using Aspose.Words.Fonts;
using Aspose.Words.Tables;
using ContractFieldData = Aspose.Cli.Product.Words.Contracts.FieldData;
using ContractImageData = Aspose.Cli.Product.Words.Contracts.ImageData;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

internal static class InfoProjection
{
    public static DocumentInfoResult Project(LoadedDocument loaded, string path, DocumentInfoRequest request)
    {
        Document document = loaded.Document;
        var index = new DocumentBlockIndex(document);
        HashSet<string> details = request.Details?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        NodeCollection paragraphs = document.GetChildNodes(NodeType.Paragraph, true);
        NodeCollection tables = document.GetChildNodes(NodeType.Table, true);
        document.UpdateWordCount();

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
            Outline = details.Contains("outline") || request.IncludePreview ? Outline(index) : null,
            Styles = details.Contains("styles") ? document.Styles.Cast<Style>()
                .Select(static s => s.Name).Order(StringComparer.Ordinal).ToArray() : null,
            Fields = details.Contains("fields") ? Fields(document, index) : null,
            Bookmarks = details.Contains("bookmarks") ? document.Range.Bookmarks.Cast<Bookmark>()
                .Select(static b => b.Name).Order(StringComparer.Ordinal).ToArray() : null,
            Comments = details.Contains("comments") ? Comments(document, index) : null,
            Images = details.Contains("images") ? Images(document, index) : null,
            Tables = details.Contains("tables") ? Tables(index) : null,
            Properties = details.Contains("properties") ? Properties(document) : null,
            Fonts = details.Contains("fonts") ? document.FontInfos.Cast<FontInfo>()
                .Select(static f => f.Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() : null,
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

    private static IReadOnlyList<OutlineItem> Outline(DocumentBlockIndex index) =>
        index.Entries.Where(static entry => entry.Node is Paragraph p && HeadingLevel(p) is not null)
            .Select(static entry =>
            {
                var paragraph = (Paragraph)entry.Node;
                return new OutlineItem
                {
                    Block = entry.Index,
                    Level = HeadingLevel(paragraph)!.Value,
                    Text = Clean(paragraph.GetText()),
                };
            }).Take(1000).ToArray();

    private static IReadOnlyList<ContractFieldData> Fields(Document document, DocumentBlockIndex index) =>
        document.Range.Fields.Cast<Field>().Select(field => new ContractFieldData
        {
            Type = field.Type.ToString(),
            Block = index.FindBlock(field.Start) ?? 0,
            Code = field.GetFieldCode(),
            Result = field.Result,
        }).Take(1000).ToArray();

    private static IReadOnlyList<CommentData> Comments(Document document, DocumentBlockIndex index) =>
        document.GetChildNodes(NodeType.Comment, true).Cast<Comment>().Select(comment => new CommentData
        {
            Author = comment.Author,
            Text = Clean(comment.GetText()),
            Block = index.FindBlock(comment),
        }).Take(1000).ToArray();

    private static IReadOnlyList<ContractImageData> Images(Document document, DocumentBlockIndex index) =>
        document.GetChildNodes(NodeType.Shape, true).Cast<Shape>().Where(static shape => shape.HasImage)
            .Select(shape => new ContractImageData
            {
                Block = index.FindBlock(shape) ?? 0,
                Name = shape.Name,
                WidthPoints = shape.Width,
                HeightPoints = shape.Height,
            }).Take(1000).ToArray();

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

    internal static string Clean(string value) => value.TrimEnd('\r', '\a', '\f').Replace("\u0007", string.Empty, StringComparison.Ordinal);
}
