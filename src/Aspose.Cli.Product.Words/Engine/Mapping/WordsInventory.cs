using Aspose.Cli.Sdk.Tables;
using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Notes;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

/// <summary>
/// The inventory of a document: how many nodes of each kind it holds, counted with the engine's
/// Document.GetChildNodes(type, true) and its collections, and whether <c>read</c> lists them. No
/// count lays the document out: the engine's page count would, and a layout changes what the
/// document saves.
/// </summary>
internal static class WordsInventory
{
    /// <summary>The kinds the inventory counts, in the order a result lists them; read once the table's kinds exist.</summary>
    internal static IReadOnlyList<InventoryKind> Kinds => Declared.Value;

    private static class Declared
    {
        internal static readonly IReadOnlyList<InventoryKind> Value =
        [
            new("sections", "The sections: Document.Sections.Count.", WordsTable.SectionKind),
            new("headersFooters", "The headers and footers of every section: NodeType.HeaderFooter.", WordsTable.HeaderFooterKind),
            new("paragraphs", "The paragraphs of every story: NodeType.Paragraph.", WordsTable.ParagraphKind),
            new("tables", "The tables, nested ones included: NodeType.Table.", WordsTable.TableKind),
            new("rows", "The rows of every table: NodeType.Row.", WordsTable.RowKind),
            new("cells", "The cells of every table: NodeType.Cell.", WordsTable.CellKind),
            new("runs", "The runs of text, those inside an equation included, which read does not list: NodeType.Run.", WordsTable.RunKind),
            new("fields", "The fields: Document.Range.Fields.Count.", WordsTable.FieldKind),
            new("shapes", "The shapes, pictures and text boxes, those inside a group shape included, which read does not list: NodeType.Shape.", WordsTable.ShapeKind),
            new("structuredDocumentTags", "The content controls: NodeType.StructuredDocumentTag.", WordsTable.SdtKind),
            new("comments", "The comments: NodeType.Comment.", WordsTable.CommentKind),
            new("footnotes", "The footnotes: NodeType.Footnote whose FootnoteType is Footnote.", WordsTable.FootnoteKind),
            new("endnotes", "The endnotes: NodeType.Footnote whose FootnoteType is Endnote.", WordsTable.FootnoteKind),
            new("bookmarks", "The bookmarks: Document.Range.Bookmarks.Count.", WordsTable.BookmarkKind),
            new("styles", "The styles: Document.Styles.Count.", WordsTable.StyleKind),
            new("lists", "The list definitions: Document.Lists.Count.", WordsTable.ListKind),
            new("revisions", "The revisions: Document.Revisions.Count.", WordsTable.RevisionKind),
            new("customProperties", "The custom document properties: Document.CustomDocumentProperties.Count.", WordsTable.CustomPropertyKind),
            new("groupShapes", "The group shapes: NodeType.GroupShape.", Kind: null),
            new("charts", "The shapes whose Shape.HasChart is true.", Kind: null),
            new("officeMath", "The equations: NodeType.OfficeMath.", Kind: null),
            new("formFields", "The form fields: NodeType.FormField.", Kind: null),
            new("smartTags", "The smart tags: NodeType.SmartTag.", Kind: null),
            new("subDocuments", "The links to subdocuments: NodeType.SubDocument.", Kind: null),
            new("editableRanges", "The ranges editable in a protected document: NodeType.EditableRangeStart.", Kind: null),
            new("glossaryDocuments", "1 when Document.GlossaryDocument, the building blocks, is not null, else 0.", Kind: null),
            new("vbaProjects", "1 when Document.HasMacros is true, else 0.", Kind: null),
        ];
    }

    /// <summary>Counts every kind.</summary>
    internal static IReadOnlyDictionary<string, long> Count(Document document)
    {
        long Nodes(NodeType type) => document.GetChildNodes(type, true).Count;
        Footnote[] notes = [.. document.GetChildNodes(NodeType.Footnote, true).Cast<Footnote>()];
        return new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["sections"] = document.Sections.Count,
            ["headersFooters"] = Nodes(NodeType.HeaderFooter),
            ["paragraphs"] = Nodes(NodeType.Paragraph),
            ["tables"] = Nodes(NodeType.Table),
            ["rows"] = Nodes(NodeType.Row),
            ["cells"] = Nodes(NodeType.Cell),
            ["runs"] = Nodes(NodeType.Run),
            ["fields"] = document.Range.Fields.Count,
            ["shapes"] = Nodes(NodeType.Shape),
            ["structuredDocumentTags"] = Nodes(NodeType.StructuredDocumentTag),
            ["comments"] = Nodes(NodeType.Comment),
            ["footnotes"] = notes.LongCount(static note => note.FootnoteType == FootnoteType.Footnote),
            ["endnotes"] = notes.LongCount(static note => note.FootnoteType == FootnoteType.Endnote),
            ["bookmarks"] = document.Range.Bookmarks.Count,
            ["styles"] = document.Styles.Count,
            ["lists"] = document.Lists.Count,
            ["revisions"] = document.Revisions.Count,
            ["customProperties"] = document.CustomDocumentProperties.Count,
            ["groupShapes"] = Nodes(NodeType.GroupShape),
            ["charts"] = document.GetChildNodes(NodeType.Shape, true).Cast<Shape>().LongCount(static shape => shape.HasChart),
            ["officeMath"] = Nodes(NodeType.OfficeMath),
            ["formFields"] = Nodes(NodeType.FormField),
            ["smartTags"] = Nodes(NodeType.SmartTag),
            ["subDocuments"] = Nodes(NodeType.SubDocument),
            ["editableRanges"] = Nodes(NodeType.EditableRangeStart),
            ["glossaryDocuments"] = document.GlossaryDocument is null ? 0 : 1,
            ["vbaProjects"] = document.HasMacros ? 1 : 0,
        };
    }
}
