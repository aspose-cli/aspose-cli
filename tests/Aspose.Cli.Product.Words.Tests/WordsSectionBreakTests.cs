using Aspose.Words;
using Aspose.Words.Saving;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

public sealed class WordsSectionBreakTests
{
    [Theory]
    [InlineData("after", new[] { "One", "Two" }, new[] { "Three" })]
    [InlineData("before", new[] { "One" }, new[] { "Two", "Three" })]
    public void SectionBreak_SplitsTheSectionAtTheAnchorAndKeepsItsPageSetup(
        string position, string[] first, string[] second)
    {
        using var fixture = new WordsFixture();
        string input = CreateA4(fixture);
        string output = fixture.Temp.File($"split-{position}.docx");

        fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops = [new InsertBreakOp { At = new WordsTarget { Find = "Two" }, Position = position, Kind = "section" }],
        }, new WordsEditRequest { OutputPath = output });

        var document = new Document(output);
        Assert.Equal(2, document.Sections.Count);
        Assert.Equal(first, Texts(document.Sections[0]));
        Assert.Equal(second, Texts(document.Sections[1]));
        Assert.All(document.Sections.Cast<Section>(), static section =>
        {
            Assert.Equal(PaperSize.A4, section.PageSetup.PaperSize);
            Assert.Equal(Orientation.Landscape, section.PageSetup.Orientation);
        });
        // Without its own header the new section continues the first section's.
        Assert.Null(document.Sections[1].HeadersFooters[HeaderFooterType.HeaderPrimary]);
    }

    [Fact]
    public void AddSection_TakesItsNeighboursPageSetup()
    {
        using var fixture = new WordsFixture();
        string input = CreateA4(fixture);
        string output = fixture.Temp.File("added.docx");

        fixture.Engine.ApplyOps(input, new WordsOpsBatch { Ops = [new AddSectionOp()] }, new WordsEditRequest { OutputPath = output });

        var document = new Document(output);
        Section added = document.LastSection;
        Assert.Equal(2, document.Sections.Count);
        Assert.Equal(PaperSize.A4, added.PageSetup.PaperSize);
        Assert.Equal(Orientation.Landscape, added.PageSetup.Orientation);
    }

    private static string[] Texts(Section section) =>
        section.Body.Paragraphs.Cast<Paragraph>()
            .Select(static paragraph => paragraph.GetText().Trim())
            .Where(static text => text.Length > 0 && !text.Contains("evaluation", StringComparison.OrdinalIgnoreCase))
            .ToArray();

    private static string CreateA4(WordsFixture fixture)
    {
        fixture.Gate.EnsureApplied();
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.PageSetup.PaperSize = PaperSize.A4;
        builder.PageSetup.Orientation = Orientation.Landscape;
        builder.MoveToHeaderFooter(HeaderFooterType.HeaderPrimary);
        builder.Write("Header");
        builder.MoveToDocumentEnd();
        builder.Writeln("One");
        builder.Writeln("Two");
        builder.Write("Three");
        string path = fixture.Temp.File("a4.docx");
        document.Save(path, SaveFormat.Docx);
        return path;
    }
}
