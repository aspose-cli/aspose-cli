using System.IO.Compression;
using Aspose.Cli.Sdk.Errors;
using Aspose.Words;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

public sealed class WordsTrackChangesTests
{
    [Fact]
    public void TrackedBatch_WithAnUntrackableOperation_IsRejectedBeforeAnyChange()
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateReport();
        string output = fixture.Temp.File("tracked.docx");

        CliException error = Assert.Throws<CliException>(() => fixture.Engine.ApplyOps(
            input,
            new WordsOpsBatch
            {
                Ops =
                [
                    new ReplaceTextOp { Find = "twelve", Replace = "fifteen" },
                    new FormatTextOp { Target = new WordsTarget { Block = 1 }, Bold = true },
                    new SetPageSetupOp { Setup = new PageSetupInput { Orientation = "landscape" } },
                ],
            },
            new WordsEditRequest { OutputPath = output, TrackChanges = true, Author = "Reviewer" }));

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.Contains("format_text, set_page_setup", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void TrackedBatch_OfContentEdits_RecordsRevisions()
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateReport();
        string output = fixture.Temp.File("tracked.docx");

        fixture.Engine.ApplyOps(
            input,
            new WordsOpsBatch
            {
                Ops =
                [
                    new ReplaceTextOp { Find = "twelve", Replace = "fifteen" },
                    new AddCommentOp { At = new WordsTarget { Block = 1 }, Author = "Reviewer", Text = "Check" },
                ],
            },
            new WordsEditRequest { OutputPath = output, TrackChanges = true, Author = "Reviewer" });

        var document = new Document(output);
        Assert.True(document.HasRevisions);
        Assert.All(document.Revisions, static revision => Assert.Equal("Reviewer", revision.Author));
    }

    [Fact]
    public void InspectRevisions_ListsEachChangeInDocumentOrder()
    {
        using var fixture = new WordsFixture();
        string input = fixture.Temp.File("reviewed.docx");
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Writeln("The notice period is thirty days.");
        builder.Write("This clause is removed.");
        document.StartTrackRevisions("Alice Legal", new DateTime(2026, 9, 1, 10, 30, 0));
        document.Range.Replace("thirty", "sixty");
        document.StopTrackRevisions();
        document.StartTrackRevisions("Bob Counsel", new DateTime(2026, 9, 2, 8, 0, 0));
        ((Paragraph)document.FirstSection.Body.Paragraphs[1]).Runs[0].Remove();
        builder.MoveToDocumentEnd();
        builder.Write("New ");
        builder.Font.Bold = true;
        builder.Write("governing law");
        builder.Font.Bold = false;
        builder.Write(" clause.");
        document.StopTrackRevisions();
        document.Save(input);

        DocumentInfoResult info = fixture.Engine.GetInfo(input, new DocumentInfoRequest { Details = ["revisions"] });

        Assert.Equal(
            [
                new RevisionData { Type = "deletion", Author = "Alice Legal", Date = "2026-09-01T10:30:00", Block = 1, Text = "thirty" },
                new RevisionData { Type = "insertion", Author = "Alice Legal", Date = "2026-09-01T10:30:00", Block = 1, Text = "sixty" },
                new RevisionData { Type = "deletion", Author = "Bob Counsel", Date = "2026-09-02T08:00:00", Block = 2, Text = "This clause is removed." },
                new RevisionData { Type = "insertion", Author = "Bob Counsel", Date = "2026-09-02T08:00:00", Block = 2, Text = "New governing law clause." },
            ],
            info.Revisions);
    }

    [Fact]
    public void InspectRevisions_ListsAMoveOnceAtEachSideAndAParagraphMarkWithoutItsText()
    {
        using var fixture = new WordsFixture();
        const string Ann = """w:author="Ann" w:date="2026-09-01T10:00:00Z" """;
        const string Bob = """w:author="Bob" w:date="2026-09-02T08:00:00Z" """;
        const string Passage = """<w:r><w:t xml:space="preserve">Alpha </w:t></w:r><w:r><w:rPr><w:b/></w:rPr><w:t>beta</w:t></w:r><w:r><w:t xml:space="preserve"> gamma.</w:t></w:r>""";
        const string Paragraphs = """<w:r><w:t xml:space="preserve">Whole </w:t></w:r><w:r><w:rPr><w:i/></w:rPr><w:t>para</w:t></w:r>""";
        string input = WriteDocx(fixture.Temp.File("moved.docx"), $"""
            <w:p><w:r><w:t>Intro.</w:t></w:r></w:p>
            <w:p><w:moveFromRangeStart w:id="1" {Ann} w:name="move1"/><w:moveFrom w:id="2" {Ann}>{Passage}</w:moveFrom><w:moveFromRangeEnd w:id="1"/><w:r><w:t xml:space="preserve"> Stays.</w:t></w:r></w:p>
            <w:p><w:r><w:t xml:space="preserve">Middle. </w:t></w:r><w:moveToRangeStart w:id="3" {Ann} w:name="move1"/><w:moveTo w:id="4" {Ann}>{Passage}</w:moveTo><w:moveToRangeEnd w:id="3"/></w:p>
            <w:p><w:pPr><w:rPr><w:ins w:id="5" {Bob}/></w:rPr></w:pPr><w:r><w:t>Split here</w:t></w:r></w:p>
            <w:p><w:pPr><w:rPr><w:del w:id="6" {Bob}/></w:rPr></w:pPr><w:r><w:t>Joined</w:t></w:r></w:p>
            <w:p><w:r><w:t>Tail.</w:t></w:r></w:p>
            <w:p><w:pPr><w:rPr><w:moveFrom w:id="7" {Ann}/></w:rPr></w:pPr><w:moveFromRangeStart w:id="8" {Ann} w:name="move2"/><w:moveFrom w:id="9" {Ann}>{Paragraphs}</w:moveFrom></w:p>
            <w:p><w:pPr><w:rPr><w:moveFrom w:id="10" {Ann}/></w:rPr></w:pPr><w:moveFrom w:id="11" {Ann}><w:r><w:t>Second</w:t></w:r></w:moveFrom><w:moveFromRangeEnd w:id="8"/></w:p>
            <w:p><w:r><w:t>Between.</w:t></w:r></w:p>
            <w:p><w:pPr><w:rPr><w:moveTo w:id="12" {Ann}/></w:rPr></w:pPr><w:moveToRangeStart w:id="13" {Ann} w:name="move2"/><w:moveTo w:id="14" {Ann}>{Paragraphs}</w:moveTo></w:p>
            <w:p><w:pPr><w:rPr><w:moveTo w:id="15" {Ann}/></w:rPr></w:pPr><w:moveTo w:id="16" {Ann}><w:r><w:t>Second</w:t></w:r></w:moveTo><w:moveToRangeEnd w:id="13"/></w:p>
            <w:p><w:r><w:t>End.</w:t></w:r></w:p>
            """);

        DocumentInfoResult info = fixture.Engine.GetInfo(input, new DocumentInfoRequest { Details = ["revisions"] });

        const string AnnDate = "2026-09-01T10:00:00";
        const string BobDate = "2026-09-02T08:00:00";
        Assert.Equal(
            [
                new RevisionData { Type = "moving", Author = "Ann", Date = AnnDate, Block = 2, Text = "Alpha beta gamma." },
                new RevisionData { Type = "moving", Author = "Ann", Date = AnnDate, Block = 3, Text = "Alpha beta gamma." },
                new RevisionData { Type = "insertion", Author = "Bob", Date = BobDate, Block = 4 },
                new RevisionData { Type = "deletion", Author = "Bob", Date = BobDate, Block = 5 },
                new RevisionData { Type = "moving", Author = "Ann", Date = AnnDate, Block = 7, Text = "Whole para\rSecond" },
                new RevisionData { Type = "moving", Author = "Ann", Date = AnnDate, Block = 10, Text = "Whole para\rSecond" },
            ],
            info.Revisions);
    }

    // Aspose.Words has no public API that records a move, so the moves are written as WordprocessingML.
    private static string WriteDocx(string path, string body)
    {
        using (var package = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            void Add(string name, string content)
            {
                using var writer = new StreamWriter(package.CreateEntry(name).Open());
                writer.Write(content);
            }

            Add("[Content_Types].xml", """<?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>""");
            Add("_rels/.rels", """<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>""");
            Add("word/document.xml", $"""<?xml version="1.0" encoding="UTF-8"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>{body}</w:body></w:document>""");
        }

        return path;
    }
}
