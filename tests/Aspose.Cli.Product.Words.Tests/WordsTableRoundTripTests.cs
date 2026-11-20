using System.Text.Json.Nodes;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.TestKit.Tables;
using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Lists;
using Aspose.Words.Markup;
using Aspose.Words.Notes;
using Aspose.Words.Saving;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// The Words table's round trip, one case per row: a settable row is set through an operation
/// document to a value other than the one it holds, the edit saves and reopens the file, and the
/// value its report reads back equals the one written; a read-only row is read. The document is
/// made by the engine with one object of every kind; a row the engine does not keep is listed
/// with its known issue, and the case checks that the engine still does not keep it.
/// </summary>
public sealed class WordsTableRoundTripTests
{
    public static TheoryData<string, string> Settable => TableRoundTrip.Cases(WordsTable.Definition, settable: true);

    public static TheoryData<string, string> ReadOnly => TableRoundTrip.Cases(WordsTable.Definition, settable: false);

    /// <summary>The rows whose sample is the document's or the member's, not the value kind's.</summary>
    private static readonly Dictionary<(string Kind, string Row), JsonNode> Samples = new()
    {
        [("section", "pageSetup.pageWidth")] = 600,
        [("section", "pageSetup.pageHeight")] = 800,
        [("section", "pageSetup.topMargin")] = 50,
        [("section", "pageSetup.rightMargin")] = 50,
        [("section", "pageSetup.bottomMargin")] = 50,
        [("section", "pageSetup.leftMargin")] = 50,
        [("section", "pageSetup.headerDistance")] = 30,
        [("section", "pageSetup.footerDistance")] = 30,
        [("section", "pageSetup.pageStartingNumber")] = 5,
        [("paragraph", "paragraphFormat.styleName")] = "Heading 2",
        [("paragraph", "paragraphFormat.lineSpacing")] = 18,
        [("paragraph", "listFormat.list")] = 1,
        [("paragraph", "listFormat.listLevelNumber")] = 1,
        [("run", "font.name")] = "Arial",
        [("run", "font.nameAscii")] = "Arial",
        [("run", "font.nameOther")] = "Arial",
        [("run", "font.nameFarEast")] = "SimSun",
        [("run", "font.size")] = 14,
        [("run", "font.highlightColor")] = "#FFFF00",
        [("run", "font.styleName")] = "Trip Character",
        [("field", "address")] = "https://example.com/trip",
        [("field", "headingLevelRange")] = "1-2",
        [("shape", "width")] = 40,
        [("shape", "height")] = 30,
        [("table", "styleName")] = "Trip Table",
        [("row", "rowFormat.height")] = 30,
        [("cell", "cellFormat.width")] = 120,
        [("comment", "dateTime")] = "2026-03-01T09:30:00",
        [("footnote", "referenceMark")] = "*",
        [("style", "name")] = "Trip Renamed",
        [("style", "baseStyleName")] = "Heading 1",
        [("style", "nextParagraphStyleName")] = "Heading 2",
        [("style", "font.name")] = "Arial",
        [("style", "font.nameAscii")] = "Arial",
        [("style", "font.nameOther")] = "Arial",
        [("style", "font.nameFarEast")] = "SimSun",
        [("style", "font.size")] = 13,
        [("style", "font.bold")] = true,
        [("style", "font.italic")] = true,
        [("style", "font.color")] = "#12AB34",
        [("style", "paragraphFormat.spaceBefore")] = 7,
        [("style", "paragraphFormat.spaceAfter")] = 9,
        [("listLevel", "numberFormat")] = "\u0000)",
        [("listLevel", "startAt")] = 3,
        [("customProperty", "value")] = "Trip",
    };

    /// <summary>The rows the same operation sets before the row under test, so it is written where the engine keeps it.</summary>
    private static readonly Dictionary<(string Kind, string Row), JsonObject> Setup = new()
    {
        [("paragraph", "listFormat.listLevelNumber")] = new() { ["listFormat.list"] = 1 },
        [("row", "rowFormat.heightRule")] = new() { ["rowFormat.height"] = 30 },
        [("section", "pageSetup.pageStartingNumber")] = new() { ["pageSetup.restartPageNumbering"] = true },
    };

    /// <summary>The objects a row is written on when the first object that has it is not one the row can be set on.</summary>
    private static readonly Dictionary<(string Kind, string Row), string> Addresses = new()
    {
        [("style", "name")] = "style/Trip Paragraph",
        [("style", "baseStyleName")] = "style/Trip Paragraph",
        [("style", "nextParagraphStyleName")] = "style/Trip Paragraph",
        [("style", "font.name")] = "style/Trip Paragraph",
        [("style", "font.nameAscii")] = "style/Trip Paragraph",
        [("style", "font.nameOther")] = "style/Trip Paragraph",
        [("style", "font.nameFarEast")] = "style/Trip Paragraph",
        [("style", "font.size")] = "style/Trip Paragraph",
        [("style", "font.bold")] = "style/Trip Paragraph",
        [("style", "font.italic")] = "style/Trip Paragraph",
        [("style", "font.color")] = "style/Trip Paragraph",
        [("style", "paragraphFormat.spaceBefore")] = "style/Trip Paragraph",
        [("style", "paragraphFormat.spaceAfter")] = "style/Trip Paragraph",
        [("range", "text")] = "section/0/body/range",
        [("layout", "pageCount")] = "layout",
        [("listLevel", "numberStyle")] = "list/1/level/0",
        [("listLevel", "numberFormat")] = "list/1/level/0",
        [("listLevel", "startAt")] = "list/1/level/0",
    };

    /// <summary>
    /// The rows the engine does not keep as written on one object, each with why: a merge is kept
    /// with the cell it pairs with, which <see cref="WordsTableBehaviorTests.MergedCells_AreKeptAsAPair"/> sets.
    /// </summary>
    private static readonly Dictionary<(string Kind, string Row), string> EngineDifferences = new()
    {
        [("cell", "cellFormat.horizontalMerge")] = "a merge is kept with the cell it pairs with",
        [("cell", "cellFormat.verticalMerge")] = "a merge is kept with the cell it pairs with",
    };

    [Theory]
    [MemberData(nameof(Settable))]
    public void SettableRow_ReadsBackFromTheSavedFile(string kind, string row)
    {
        WordsFixture.RequireLicense();
        using var fixture = new WordsFixture();
        string input = Rich(fixture);
        var document = new Document(input);
        string at = Address(document, kind, row);
        JsonNode? current = TableRoundTrip.Read(WordsTable.Definition, document, at)[row]?.DeepClone();
        JsonNode write = Samples.TryGetValue((kind, row), out JsonNode? special)
            ? special.DeepClone()
            : TableRoundTrip.Sample(TableRoundTrip.ValueOf(WordsTable.Definition, kind, row), current);
        JsonObject props = Setup.TryGetValue((kind, row), out JsonObject? setup) ? setup.DeepClone().AsObject() : [];
        props[row] = write.DeepClone();
        string ops = new JsonObject { ["ops"] = new JsonArray(new JsonObject { ["set"] = at, ["props"] = props }) }.ToJsonString();
        JsonNode? now = null;
        Exception? failed = null;
        try
        {
            WordsEditResult result = WordsEdit.Run(fixture.Session, WordsFixture.Edit(input, fixture.File("out.docx"), ops));
            now = Assert.Single(result.Ops).Now![row];
        }
        catch (Exception exception) when (EngineDifferences.ContainsKey((kind, row)))
        {
            failed = exception;
        }

        bool kept = failed is null && JsonNode.DeepEquals(now, write);
        if (EngineDifferences.TryGetValue((kind, row), out string? issue))
        {
            Assert.False(kept, $"{kind}.{row} now reads back as written alone; it was listed because {issue}.");
            return;
        }

        Assert.True(kept, $"{kind}.{row} at {at}: wrote {write.ToJsonString()}, read back {now?.ToJsonString() ?? "null"} (was {current?.ToJsonString() ?? "null"}).");
    }

    [Theory]
    [MemberData(nameof(ReadOnly))]
    public void ReadOnlyRow_IsRead(string kind, string row)
    {
        WordsFixture.RequireLicense();
        using var fixture = new WordsFixture();
        var document = new Document(Rich(fixture));

        Assert.True(TableRoundTrip.Read(WordsTable.Definition, document, Address(document, kind, row)).ContainsKey(row));
    }

    /// <summary>
    /// A document with one object of every kind: two sections, a primary header and footer, styled
    /// paragraphs, a list, a table, a picture and a text box, a content control, PAGE, HYPERLINK and
    /// TOC fields, a bookmark, a comment, a footnote, a tracked revision and a custom property.
    /// </summary>
    internal static string Rich(WordsFixture fixture, string name = "rich.docx")
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        document.Styles.Add(StyleType.Paragraph, "Trip Paragraph");
        document.Styles.Add(StyleType.Character, "Trip Character");
        document.Styles.Add(StyleType.Table, "Trip Table");
        builder.InsertTableOfContents("\\o \"1-3\" \\h \\z \\u");
        builder.ParagraphFormat.StyleName = "Heading 1";
        builder.Writeln("Heading");
        builder.ParagraphFormat.StyleName = "Normal";
        builder.StartBookmark("Mark");
        builder.Write("Body text ");
        builder.EndBookmark("Mark");
        builder.InsertHyperlink("link", "https://example.com", false);
        builder.InsertFootnote(FootnoteType.Footnote, "Note text.");
        builder.Writeln();
        List list = document.Lists.Add(ListTemplate.NumberDefault);
        builder.ListFormat.List = list;
        builder.Writeln("Listed");
        builder.ListFormat.RemoveNumbers();
        builder.InsertImage(Png());
        Shape box = builder.InsertShape(ShapeType.TextBox, 100, 40);
        box.AppendChild(new Paragraph(document));
        box.FirstParagraph.AppendChild(new Run(document, "In the box"));
        builder.Writeln();
        builder.StartTable();
        builder.InsertCell();
        builder.Write("A1");
        builder.InsertCell();
        builder.Write("B1");
        builder.EndRow();
        builder.EndTable();
        var control = new StructuredDocumentTag(document, SdtType.RichText, MarkupLevel.Block) { Tag = "control", Title = "Control" };
        control.RemoveAllChildren();
        control.AppendChild(new Paragraph(document));
        ((Paragraph)control.FirstChild).AppendChild(new Run(document, "Controlled"));
        document.FirstSection.Body.AppendChild(control);
        var commented = document.FirstSection.Body.AppendParagraph("Commented");
        var comment = new Comment(document, "Ann", "A", new DateTime(2026, 1, 2, 3, 4, 0));
        comment.SetText("Comment text.");
        commented.AppendChild(comment);
        builder.MoveToDocumentEnd();
        builder.InsertBreak(BreakType.SectionBreakNewPage);
        builder.Writeln("Second section");
        builder.MoveToHeaderFooter(HeaderFooterType.HeaderPrimary);
        builder.Write("Header");
        builder.MoveToHeaderFooter(HeaderFooterType.FooterPrimary);
        builder.InsertField("PAGE", "1");
        document.CustomDocumentProperties.Add("Client", "Contoso");
        document.UpdateFields();
        document.StartTrackRevisions("Reviewer", new DateTime(2026, 2, 3, 4, 5, 0));
        document.FirstSection.Body.AppendParagraph("Inserted");
        document.StopTrackRevisions();
        string path = fixture.File(name);
        document.Save(path, SaveFormat.Docx);
        return path;
    }

    /// <summary>The address of an object of the kind that has the row.</summary>
    private static string Address(Document document, string kind, string row) =>
        Addresses.TryGetValue((kind, row), out string? at)
            ? at
            : TableRoundTrip.AddressOf(WordsTable.Definition, document, kind, row)
                ?? throw new InvalidOperationException($"The round-trip document has no {kind} with the row {row}.");

    /// <summary>A small PNG: one red pixel.</summary>
    internal static byte[] Png() => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
}
