using Aspose.Cli.Sdk.Tables;
using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Fields;
using Aspose.Words.Markup;
using Aspose.Words.Notes;
using Aspose.Words.Tables;
using Row = Aspose.Cli.Sdk.Tables.Row;
using WordRange = Aspose.Words.Range;
using WordRow = Aspose.Words.Tables.Row;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

/// <summary>
/// The Words object table: one name for each Aspose.Words member a read gets and a set sets,
/// where each kind of object sits in the engine's node tree, and the engine calls that add, delete
/// and clone objects. An object is addressed by its position, from 0, in its parent's own
/// collection of its kind (a story's Paragraphs, Tables, a paragraph's Runs); styles, bookmarks
/// and custom properties by their names, lists by their ListId, headers and footers by their
/// HeaderFooterType.
/// </summary>
internal static partial class WordsTable
{
    /// <summary>The document itself, addressed as <c>document</c>.</summary>
    internal static readonly ObjectKind<Document> DocumentKind = new("document", "The document.")
    {
        Row.Text<Document>("builtInDocumentProperties.title", "Document.BuiltInDocumentProperties.Title", static d => d.BuiltInDocumentProperties.Title, static (d, v) => d.BuiltInDocumentProperties.Title = v),
        Row.Text<Document>("builtInDocumentProperties.author", "Document.BuiltInDocumentProperties.Author", static d => d.BuiltInDocumentProperties.Author, static (d, v) => d.BuiltInDocumentProperties.Author = v),
        Row.Text<Document>("builtInDocumentProperties.subject", "Document.BuiltInDocumentProperties.Subject", static d => d.BuiltInDocumentProperties.Subject, static (d, v) => d.BuiltInDocumentProperties.Subject = v),
        Row.Text<Document>("builtInDocumentProperties.keywords", "Document.BuiltInDocumentProperties.Keywords", static d => d.BuiltInDocumentProperties.Keywords, static (d, v) => d.BuiltInDocumentProperties.Keywords = v),
        Row.Integer<Document>("builtInDocumentProperties.words", "Document.BuiltInDocumentProperties.Words: the count the file holds, which the update_word_count action recounts", static d => d.BuiltInDocumentProperties.Words),
        Row.Enum<Document, ProtectionType>("protectionType", "Document.ProtectionType; the protect and unprotect actions change it", static d => d.ProtectionType),
        Row.Enum<Document, WatermarkType>("watermark.type", "Document.Watermark.Type; the watermark actions change it", static d => d.Watermark.Type),
        Row.Boolean<Document>("trackRevisions", "Document.TrackRevisions: whether Microsoft Word tracks the changes made to the document", static d => d.TrackRevisions, static (d, v) => d.TrackRevisions = v),
        Row.Boolean<Document>("hasRevisions", "Document.HasRevisions", static d => d.HasRevisions),
        Row.Boolean<Document>("hasMacros", "Document.HasMacros", static d => d.HasMacros),
    };

    /// <summary>The document as the engine lays it out on pages: <c>layout</c>, read only by --at.</summary>
    internal static readonly ObjectKind<Document> LayoutKind = new("layout", "The document laid out on pages; read does not list it, --at layout reads it, and reading it makes the engine lay the document out.")
    {
        Row.Integer<Document>("pageCount", "Document.PageCount: the engine lays the document out to count its pages", static d => d.PageCount),
    };

    /// <summary>A section: <c>section/0</c>.</summary>
    internal static readonly ObjectKind<Section> SectionKind = new("section", "A section of the document: Document.Sections[i].")
    {
        Row.Number<Section>("pageSetup.pageWidth", "Section.PageSetup.PageWidth, in points", static s => s.PageSetup.PageWidth, static (s, v) => s.PageSetup.PageWidth = v),
        Row.Number<Section>("pageSetup.pageHeight", "Section.PageSetup.PageHeight, in points", static s => s.PageSetup.PageHeight, static (s, v) => s.PageSetup.PageHeight = v),
        Row.Enum<Section, Orientation>("pageSetup.orientation", "Section.PageSetup.Orientation; the engine swaps the width and height when it changes", static s => s.PageSetup.Orientation, static (s, v) => s.PageSetup.Orientation = v),
        Row.Number<Section>("pageSetup.topMargin", "Section.PageSetup.TopMargin, in points", static s => s.PageSetup.TopMargin, static (s, v) => s.PageSetup.TopMargin = v),
        Row.Number<Section>("pageSetup.rightMargin", "Section.PageSetup.RightMargin, in points", static s => s.PageSetup.RightMargin, static (s, v) => s.PageSetup.RightMargin = v),
        Row.Number<Section>("pageSetup.bottomMargin", "Section.PageSetup.BottomMargin, in points", static s => s.PageSetup.BottomMargin, static (s, v) => s.PageSetup.BottomMargin = v),
        Row.Number<Section>("pageSetup.leftMargin", "Section.PageSetup.LeftMargin, in points", static s => s.PageSetup.LeftMargin, static (s, v) => s.PageSetup.LeftMargin = v),
        Row.Number<Section>("pageSetup.headerDistance", "Section.PageSetup.HeaderDistance, in points", static s => s.PageSetup.HeaderDistance, static (s, v) => s.PageSetup.HeaderDistance = v),
        Row.Number<Section>("pageSetup.footerDistance", "Section.PageSetup.FooterDistance, in points", static s => s.PageSetup.FooterDistance, static (s, v) => s.PageSetup.FooterDistance = v),
        Row.Integer<Section>("pageSetup.textColumns.count", "Section.PageSetup.TextColumns.Count; the set_column_count action changes it", static s => s.PageSetup.TextColumns.Count),
        Row.Boolean<Section>("pageSetup.differentFirstPageHeaderFooter", "Section.PageSetup.DifferentFirstPageHeaderFooter", static s => s.PageSetup.DifferentFirstPageHeaderFooter, static (s, v) => s.PageSetup.DifferentFirstPageHeaderFooter = v),
        Row.Boolean<Section>("pageSetup.oddAndEvenPagesHeaderFooter", "Section.PageSetup.OddAndEvenPagesHeaderFooter", static s => s.PageSetup.OddAndEvenPagesHeaderFooter, static (s, v) => s.PageSetup.OddAndEvenPagesHeaderFooter = v),
        Row.Boolean<Section>("pageSetup.restartPageNumbering", "Section.PageSetup.RestartPageNumbering", static s => s.PageSetup.RestartPageNumbering, static (s, v) => s.PageSetup.RestartPageNumbering = v),
        Row.Integer<Section>("pageSetup.pageStartingNumber", "Section.PageSetup.PageStartingNumber", static s => s.PageSetup.PageStartingNumber, static (s, v) => s.PageSetup.PageStartingNumber = v),
        Row.Enum<Section, NumberStyle>("pageSetup.pageNumberStyle", "Section.PageSetup.PageNumberStyle", static s => s.PageSetup.PageNumberStyle, static (s, v) => s.PageSetup.PageNumberStyle = v),
        Row.Enum<Section, SectionStart>("pageSetup.sectionStart", "Section.PageSetup.SectionStart", static s => s.PageSetup.SectionStart, static (s, v) => s.PageSetup.SectionStart = v),
    };

    /// <summary>A section's header or footer, addressed by its type: <c>section/0/headerFooter/FooterPrimary</c>.</summary>
    internal static readonly ObjectKind<HeaderFooter> HeaderFooterKind = new("headerFooter", "A header or footer of a section, a story: Section.HeadersFooters[HeaderFooterType]. A section without one of a type shows the previous section's.")
    {
        Row.Enum<HeaderFooter, HeaderFooterType>("headerFooterType", "HeaderFooter.HeaderFooterType", static h => h.HeaderFooterType),
        Row.Boolean<HeaderFooter>("isLinkedToPrevious", "HeaderFooter.IsLinkedToPrevious: the engine gives true for one that holds no paragraph", static h => h.IsLinkedToPrevious),
    };

    /// <summary>A section's body: <c>section/0/body</c>.</summary>
    internal static readonly ObjectKind<Body> BodyKind = new("body", "The main text of a section, a story: Section.Body.")
    {
    };

    /// <summary>The text of a story, cell or the document: <c>section/0/body/range</c>.</summary>
    internal static readonly ObjectKind<WordRange> RangeKind = new("range", "The text of an object as one string, Node.Range; read does not list ranges, --at reads one.")
    {
        Row.Text<WordRange>("text", "Range.Text: the text with the engine's control characters, such as \\r ending a paragraph, \\a ending a cell, \\u0013 \\u0014 \\u0015 around a field's code and result", static r => r.Text),
    };

    /// <summary>A paragraph: <c>section/0/body/paragraph/3</c>.</summary>
    internal static readonly ObjectKind<Paragraph> ParagraphKind = new("paragraph", "A paragraph: its parent's paragraphs, GetChildNodes(NodeType.Paragraph, false)[i], which hold those in content controls.")
    {
        Row.Text<Paragraph>("text", "Paragraph.GetText(): the paragraph's text with the engine's control characters, its deleted revisions and field codes included", static p => p.GetText()),
        Row.Text<Paragraph>("paragraphFormat.styleName", "Paragraph.ParagraphFormat.StyleName", static p => p.ParagraphFormat.StyleName, static (p, v) => p.ParagraphFormat.StyleName = v),
        Row.Enum<Paragraph, ParagraphAlignment>("paragraphFormat.alignment", "Paragraph.ParagraphFormat.Alignment", static p => p.ParagraphFormat.Alignment, static (p, v) => p.ParagraphFormat.Alignment = v),
        Row.Number<Paragraph>("paragraphFormat.leftIndent", "Paragraph.ParagraphFormat.LeftIndent, in points", static p => p.ParagraphFormat.LeftIndent, static (p, v) => p.ParagraphFormat.LeftIndent = v),
        Row.Number<Paragraph>("paragraphFormat.rightIndent", "Paragraph.ParagraphFormat.RightIndent, in points", static p => p.ParagraphFormat.RightIndent, static (p, v) => p.ParagraphFormat.RightIndent = v),
        Row.Number<Paragraph>("paragraphFormat.firstLineIndent", "Paragraph.ParagraphFormat.FirstLineIndent, in points", static p => p.ParagraphFormat.FirstLineIndent, static (p, v) => p.ParagraphFormat.FirstLineIndent = v),
        Row.Number<Paragraph>("paragraphFormat.spaceBefore", "Paragraph.ParagraphFormat.SpaceBefore, in points", static p => p.ParagraphFormat.SpaceBefore, static (p, v) => p.ParagraphFormat.SpaceBefore = v),
        Row.Number<Paragraph>("paragraphFormat.spaceAfter", "Paragraph.ParagraphFormat.SpaceAfter, in points", static p => p.ParagraphFormat.SpaceAfter, static (p, v) => p.ParagraphFormat.SpaceAfter = v),
        Row.Number<Paragraph>("paragraphFormat.lineSpacing", "Paragraph.ParagraphFormat.LineSpacing, in points; with the Multiple rule 12 is single spacing", static p => p.ParagraphFormat.LineSpacing, static (p, v) => p.ParagraphFormat.LineSpacing = v),
        Row.Enum<Paragraph, LineSpacingRule>("paragraphFormat.lineSpacingRule", "Paragraph.ParagraphFormat.LineSpacingRule", static p => p.ParagraphFormat.LineSpacingRule, static (p, v) => p.ParagraphFormat.LineSpacingRule = v),
        Row.Boolean<Paragraph>("paragraphFormat.keepWithNext", "Paragraph.ParagraphFormat.KeepWithNext", static p => p.ParagraphFormat.KeepWithNext, static (p, v) => p.ParagraphFormat.KeepWithNext = v),
        Row.Boolean<Paragraph>("paragraphFormat.keepTogether", "Paragraph.ParagraphFormat.KeepTogether", static p => p.ParagraphFormat.KeepTogether, static (p, v) => p.ParagraphFormat.KeepTogether = v),
        Row.Boolean<Paragraph>("paragraphFormat.pageBreakBefore", "Paragraph.ParagraphFormat.PageBreakBefore", static p => p.ParagraphFormat.PageBreakBefore, static (p, v) => p.ParagraphFormat.PageBreakBefore = v),
        Row.Enum<Paragraph, OutlineLevel>("paragraphFormat.outlineLevel", "Paragraph.ParagraphFormat.OutlineLevel", static p => p.ParagraphFormat.OutlineLevel, static (p, v) => p.ParagraphFormat.OutlineLevel = v),
        Row.Of<Paragraph, int?>("listFormat.list", ValueKinds.Optional(ValueKinds.Integer), "Paragraph.ListFormat.List, by its ListId; null when the paragraph is no list item. Set to a list's ListId, found with ListCollection.GetListByListId; the remove_list_numbers action removes it",
            static p => p.ListFormat.List?.ListId,
            static (p, v) => p.ListFormat.List = ListOf(p.Document, v)),
        Row.Integer<Paragraph>("listFormat.listLevelNumber", "Paragraph.ListFormat.ListLevelNumber, from 0", static p => p.ListFormat.ListLevelNumber, static (p, v) => p.ListFormat.ListLevelNumber = v),
        Row.Boolean<Paragraph>("listFormat.isListItem", "Paragraph.ListFormat.IsListItem", static p => p.ListFormat.IsListItem),
        Row.Text<Paragraph>("listLabel.labelString", "Paragraph.ListLabel.LabelString: as the engine last numbered it, which the update_list_labels action renumbers", static p => p.ListLabel.LabelString),
        Row.Boolean<Paragraph>("isInsertRevision", "Paragraph.IsInsertRevision", static p => p.IsInsertRevision),
        Row.Boolean<Paragraph>("isDeleteRevision", "Paragraph.IsDeleteRevision", static p => p.IsDeleteRevision),
        Row.Boolean<Paragraph>("isFormatRevision", "Paragraph.IsFormatRevision", static p => p.IsFormatRevision),
        Row.Boolean<Paragraph>("isMoveFromRevision", "Paragraph.IsMoveFromRevision", static p => p.IsMoveFromRevision),
        Row.Boolean<Paragraph>("isMoveToRevision", "Paragraph.IsMoveToRevision", static p => p.IsMoveToRevision),
    };

    /// <summary>A run of text: <c>…/paragraph/3/run/0</c>.</summary>
    internal static readonly ObjectKind<Run> RunKind = new("run", "A run of text with one formatting: its paragraph's runs, GetChildNodes(NodeType.Run, false)[i], which hold those in content controls. A paragraph without text has no run.")
    {
        Row.Text<Run>("text", "Run.Text", static r => r.Text, static (r, v) => r.Text = v),
        Row.Text<Run>("font.name", "Run.Font.Name; setting it sets the ASCII, other and East Asian names alike, the engine's setter", static r => r.Font.Name, static (r, v) => r.Font.Name = v),
        Row.Text<Run>("font.nameAscii", "Run.Font.NameAscii", static r => r.Font.NameAscii, static (r, v) => r.Font.NameAscii = v),
        Row.Text<Run>("font.nameOther", "Run.Font.NameOther", static r => r.Font.NameOther, static (r, v) => r.Font.NameOther = v),
        Row.Text<Run>("font.nameFarEast", "Run.Font.NameFarEast", static r => r.Font.NameFarEast, static (r, v) => r.Font.NameFarEast = v),
        Row.Number<Run>("font.size", "Run.Font.Size, in points", static r => r.Font.Size, static (r, v) => r.Font.Size = v),
        Row.Boolean<Run>("font.bold", "Run.Font.Bold", static r => r.Font.Bold, static (r, v) => r.Font.Bold = v),
        Row.Boolean<Run>("font.italic", "Run.Font.Italic", static r => r.Font.Italic, static (r, v) => r.Font.Italic = v),
        Row.Enum<Run, Underline>("font.underline", "Run.Font.Underline", static r => r.Font.Underline, static (r, v) => r.Font.Underline = v),
        Row.Color<Run>("font.color", "Run.Font.Color; null for the automatic color, Color.Empty", static r => r.Font.Color, static (r, v) => r.Font.Color = v),
        Row.Color<Run>("font.highlightColor", "Run.Font.HighlightColor; null for none, Color.Empty", static r => r.Font.HighlightColor, static (r, v) => r.Font.HighlightColor = v),
        Row.Text<Run>("font.styleName", "Run.Font.StyleName: the character style", static r => r.Font.StyleName, static (r, v) => r.Font.StyleName = v),
        Row.Boolean<Run>("isInsertRevision", "Run.IsInsertRevision", static r => r.IsInsertRevision),
        Row.Boolean<Run>("isDeleteRevision", "Run.IsDeleteRevision", static r => r.IsDeleteRevision),
        Row.Boolean<Run>("isFormatRevision", "Run.IsFormatRevision", static r => r.IsFormatRevision),
        Row.Boolean<Run>("isMoveFromRevision", "Run.IsMoveFromRevision", static r => r.IsMoveFromRevision),
        Row.Boolean<Run>("isMoveToRevision", "Run.IsMoveToRevision", static r => r.IsMoveToRevision),
    };

    /// <summary>A field, counted where it starts: <c>…/paragraph/3/field/0</c>.</summary>
    internal static readonly ObjectKind<Field> FieldKind = new("field", "A field: FieldStart.GetField() of the paragraph's own field starts, Paragraph.GetChildNodes(NodeType.FieldStart, false), in order. A field in a footnote, comment or text box anchored in the paragraph is listed below that story's paragraph, not this one.")
    {
        Row.Enum<Field, FieldType>("type", "Field.Type", static f => f.Type),
        Row.Text<Field>("fieldCode", "Field.GetFieldCode()", static f => f.GetFieldCode()),
        Row.Text<Field>("result", "Field.Result: as the engine last updated it; the update_fields and update_field actions update it", static f => f.Result, static (f, v) => f.Result = v),
        Row.Text<FieldHyperlink>("address", "FieldHyperlink.Address", static f => f.Address, static (f, v) => f.Address = v),
        Row.Text<FieldToc>("headingLevelRange", "FieldToc.HeadingLevelRange, such as 1-3", static f => f.HeadingLevelRange, static (f, v) => f.HeadingLevelRange = v),
    };

    /// <summary>A shape in a paragraph, such as a picture or a text box: <c>…/paragraph/3/shape/0</c>.</summary>
    internal static readonly ObjectKind<Shape> ShapeKind = new("shape", "A shape that is a paragraph's own child, such as a picture or a text box, whose paragraphs are below it.")
    {
        Row.Enum<Shape, ShapeType>("shapeType", "Shape.ShapeType", static s => s.ShapeType),
        Row.Number<Shape>("width", "Shape.Width, in points", static s => s.Width, static (s, v) => s.Width = v),
        Row.Number<Shape>("height", "Shape.Height, in points", static s => s.Height, static (s, v) => s.Height = v),
        Row.Enum<Shape, WrapType>("wrapType", "Shape.WrapType", static s => s.WrapType, static (s, v) => s.WrapType = v),
        Row.Boolean<Shape>("aspectRatioLocked", "Shape.AspectRatioLocked", static s => s.AspectRatioLocked, static (s, v) => s.AspectRatioLocked = v),
        Row.Boolean<Shape>("hasImage", "Shape.HasImage", static s => s.HasImage),
        Row.Text<Shape>("name", "Shape.Name", static s => s.Name, static (s, v) => s.Name = v),
    };

    /// <summary>A table: <c>section/0/body/table/0</c>.</summary>
    internal static readonly ObjectKind<Table> TableKind = new("table", "A table: its parent's Tables[i]. An empty table, or a row without cells, is not kept when the engine saves.")
    {
        Row.Text<Table>("styleName", "Table.StyleName", static t => t.StyleName, static (t, v) => t.StyleName = v),
        Row.Enum<Table, TableAlignment>("alignment", "Table.Alignment", static t => t.Alignment, static (t, v) => t.Alignment = v),
        Row.Text<Table>("title", "Table.Title", static t => t.Title, static (t, v) => t.Title = v),
        Row.Text<Table>("description", "Table.Description", static t => t.Description, static (t, v) => t.Description = v),
    };

    /// <summary>A row of a table: <c>…/table/0/row/1</c>.</summary>
    internal static readonly ObjectKind<WordRow> RowKind = new("row", "A row of a table: Table.Rows[i].")
    {
        Row.Boolean<WordRow>("rowFormat.headingFormat", "Row.RowFormat.HeadingFormat", static r => r.RowFormat.HeadingFormat, static (r, v) => r.RowFormat.HeadingFormat = v),
        Row.Boolean<WordRow>("rowFormat.allowBreakAcrossPages", "Row.RowFormat.AllowBreakAcrossPages", static r => r.RowFormat.AllowBreakAcrossPages, static (r, v) => r.RowFormat.AllowBreakAcrossPages = v),
        Row.Number<WordRow>("rowFormat.height", "Row.RowFormat.Height, in points", static r => r.RowFormat.Height, static (r, v) => r.RowFormat.Height = v),
        Row.Enum<WordRow, HeightRule>("rowFormat.heightRule", "Row.RowFormat.HeightRule", static r => r.RowFormat.HeightRule, static (r, v) => r.RowFormat.HeightRule = v),
    };

    /// <summary>A cell of a row: <c>…/row/1/cell/2</c>.</summary>
    internal static readonly ObjectKind<Cell> CellKind = new("cell", "A cell of a row, a story: Row.Cells[i]. An empty cell holds one paragraph without a run.")
    {
        Row.Number<Cell>("cellFormat.width", "Cell.CellFormat.Width, in points", static c => c.CellFormat.Width, static (c, v) => c.CellFormat.Width = v),
        Row.Enum<Cell, CellVerticalAlignment>("cellFormat.verticalAlignment", "Cell.CellFormat.VerticalAlignment", static c => c.CellFormat.VerticalAlignment, static (c, v) => c.CellFormat.VerticalAlignment = v),
        Row.Enum<Cell, CellMerge>("cellFormat.horizontalMerge", "Cell.CellFormat.HorizontalMerge", static c => c.CellFormat.HorizontalMerge, static (c, v) => c.CellFormat.HorizontalMerge = v),
        Row.Enum<Cell, CellMerge>("cellFormat.verticalMerge", "Cell.CellFormat.VerticalMerge", static c => c.CellFormat.VerticalMerge, static (c, v) => c.CellFormat.VerticalMerge = v),
        Row.Color<Cell>("cellFormat.shading.backgroundPatternColor", "Cell.CellFormat.Shading.BackgroundPatternColor; null for none, Color.Empty", static c => c.CellFormat.Shading.BackgroundPatternColor, static (c, v) => c.CellFormat.Shading.BackgroundPatternColor = v),
    };

    /// <summary>A content control: <c>section/0/body/sdt/0</c>, <c>…/paragraph/3/sdt/0</c>.</summary>
    internal static readonly ObjectKind<StructuredDocumentTag> SdtKind = new("sdt", "A content control, StructuredDocumentTag, at its place among its parent's children: around paragraphs and tables, runs, rows or cells. The engine's collections hold its content as the parent's own: read lists a paragraph in it as one of its parent's paragraphs, after the control, and its range holds the content's text. An add in a control, or an address below one, names the content by its position in the control, so the same paragraph has two addresses: read lists it by its place among the parent's paragraphs, and …/sdt/0/paragraph/1 names it in the control. A content control in a control is listed once, below the control that holds it (…/sdt/0/sdt/0): the engine's collections of content controls hold a node's direct ones only. A delete removes the control with its content, StructuredDocumentTag.Remove(); the remove_self_only action keeps the content.")
    {
        Row.Enum<StructuredDocumentTag, SdtType>("sdtType", "StructuredDocumentTag.SdtType", static s => s.SdtType),
        Row.Enum<StructuredDocumentTag, MarkupLevel>("level", "StructuredDocumentTag.Level", static s => s.Level),
        Row.Text<StructuredDocumentTag>("title", "StructuredDocumentTag.Title", static s => s.Title, static (s, v) => s.Title = v),
        Row.Text<StructuredDocumentTag>("tag", "StructuredDocumentTag.Tag", static s => s.Tag, static (s, v) => s.Tag = v),
        Row.Boolean<StructuredDocumentTag>("isShowingPlaceholderText", "StructuredDocumentTag.IsShowingPlaceholderText", static s => s.IsShowingPlaceholderText, static (s, v) => s.IsShowingPlaceholderText = v),
        Row.Boolean<StructuredDocumentTag>("lockContents", "StructuredDocumentTag.LockContents", static s => s.LockContents, static (s, v) => s.LockContents = v),
        Row.Boolean<StructuredDocumentTag>("lockContentControl", "StructuredDocumentTag.LockContentControl", static s => s.LockContentControl, static (s, v) => s.LockContentControl = v),
    };

    /// <summary>A comment, a story: <c>comment/0</c>.</summary>
    internal static readonly ObjectKind<Comment> CommentKind = new("comment", "A comment, a story: the document's comments in document order, Document.GetChildNodes(NodeType.Comment, true)[i].")
    {
        Row.Text<Comment>("author", "Comment.Author", static c => c.Author, static (c, v) => c.Author = v),
        Row.Text<Comment>("initial", "Comment.Initial", static c => c.Initial, static (c, v) => c.Initial = v),
        Row.Of<Comment, DateTime>("dateTime", WordsValues.DateTime, "Comment.DateTime", static c => c.DateTime, static (c, v) => c.DateTime = v),
        Row.Boolean<Comment>("done", "Comment.Done", static c => c.Done, static (c, v) => c.Done = v),
    };

    /// <summary>A footnote or endnote, a story: <c>footnote/0</c>.</summary>
    internal static readonly ObjectKind<Footnote> FootnoteKind = new("footnote", "A footnote or endnote, a story: the document's in document order, Document.GetChildNodes(NodeType.Footnote, true)[i].")
    {
        Row.Enum<Footnote, FootnoteType>("footnoteType", "Footnote.FootnoteType", static f => f.FootnoteType),
        Row.Text<Footnote>("referenceMark", "Footnote.ReferenceMark: empty for the automatic number", static f => f.ReferenceMark, static (f, v) => f.ReferenceMark = v),
    };

    private static Aspose.Words.Lists.List ListOf(DocumentBase document, int? id) =>
        id is { } listId
            ? document.Lists.GetListByListId(listId)
                ?? throw TableErrors.NotFound("list", listId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    [.. document.Lists.Select(static list => list.ListId.ToString(System.Globalization.CultureInfo.InvariantCulture))])
            : throw TableErrors.InvalidArguments("listFormat.list is set to a list's ListId; the remove_list_numbers action makes the paragraph no list item");
}
