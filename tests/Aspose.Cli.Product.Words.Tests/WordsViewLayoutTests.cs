using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Views;
using Aspose.Words;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// The page view places every body block on the pages it occupies, so a
/// viewer can point at exactly the paragraph or table an edit changed.
/// </summary>
public sealed class WordsViewLayoutTests : IClassFixture<WordsFixture>
{
    private readonly WordsFixture _fixture;

    public WordsViewLayoutTests(WordsFixture fixture) => _fixture = fixture;

    [Fact]
    public void RenderView_PlacesEveryBodyBlockOnItsPagesInReadingOrder()
    {
        ViewManifest view = Render(CreateDocument("layout.docx", "Middle paragraph.", bold: false));

        ViewElement[] elements = view.Parts.SelectMany(static part => part.Elements ?? []).ToArray();
        ViewElement heading = Assert.Single(elements, static element => element.Label == "Heading One");
        Assert.Equal("heading", heading.Kind);
        Assert.Equal(1, heading.Level);
        Assert.Equal("table", Assert.Single(elements, static element => element.Label == "A1 B1 A2 B2").Kind);
        ViewElement[] spanning = elements
            .Where(static element => element.Label?.StartsWith("Long paragraph", StringComparison.Ordinal) == true)
            .ToArray();
        Assert.True(spanning.Length >= 2, "The long paragraph must be placed on every page it spans.");
        Assert.Single(spanning.Select(static element => element.Digest).Distinct());
        string[] expected =
            ["Heading One", "First paragraph.", spanning[0].Label!, "A1 B1 A2 B2", "Middle paragraph.", "After page break."];
        Assert.Equal(
            expected,
            elements.Select(static element => element.Label ?? string.Empty).Distinct().ToArray());
        foreach (ViewPart part in view.Parts)
        {
            foreach (ViewElement element in part.Elements ?? [])
            {
                Assert.InRange(element.Box.X, 0, part.Width!.Value);
                Assert.InRange(element.Box.Y, 0, part.Height!.Value);
                Assert.True(element.Box.X + element.Box.Width <= part.Width!.Value + 1);
                Assert.True(element.Box.Y + element.Box.Height <= part.Height!.Value + 1);
            }
        }
    }

    [Fact]
    public void RenderView_ChangesOnlyTheDigestOfTheEditedBlock()
    {
        string[] before = Digests(Render(CreateDocument("before.docx", "Middle paragraph.", bold: false)));
        string[] after = Digests(Render(CreateDocument("after.docx", "Middle paragraph, edited.", bold: false)));

        Assert.Equal(before.Length, after.Length);
        Assert.Equal(1, before.Zip(after).Count(static pair => pair.First != pair.Second));
    }

    [Fact]
    public void RenderView_FormattingAloneChangesTheBlockDigest()
    {
        string[] plain = Digests(Render(CreateDocument("plain.docx", "Middle paragraph.", bold: false)));
        string[] bold = Digests(Render(CreateDocument("bold.docx", "Middle paragraph.", bold: true)));

        Assert.Equal(plain.Length, bold.Length);
        Assert.Equal(1, plain.Zip(bold).Count(static pair => pair.First != pair.Second));
    }

    private ViewManifest Render(string path) => _fixture.Engine.RenderView(
        path,
        new ViewRenderRequest
        {
            View = WordsViews.Pages,
            MaxParts = 10,
            Purpose = ViewPurpose.Evidence,
        },
        new DiscardingSink());

    private static string[] Digests(ViewManifest view) => view.Parts
        .SelectMany(static part => part.Elements ?? [])
        .Select(static element => element.Digest)
        .ToArray();

    private string CreateDocument(string fileName, string middle, bool bold)
    {
        _fixture.Gate.EnsureApplied();
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Heading1;
        builder.Writeln("Heading One");
        builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Normal;
        builder.Writeln("First paragraph.");
        builder.Writeln(string.Join(" ", Enumerable.Repeat("Long paragraph text", 400)));
        builder.StartTable();
        builder.InsertCell();
        builder.Write("A1");
        builder.InsertCell();
        builder.Write("B1");
        builder.EndRow();
        builder.InsertCell();
        builder.Write("A2");
        builder.InsertCell();
        builder.Write("B2");
        builder.EndRow();
        builder.EndTable();
        builder.Font.Bold = bold;
        builder.Writeln(middle);
        builder.Font.Bold = false;
        builder.InsertBreak(BreakType.PageBreak);
        builder.Write("After page break.");
        string path = _fixture.Temp.File(fileName);
        document.Save(path, SaveFormat.Docx);
        return path;
    }

    private sealed class DiscardingSink : IViewArtifactSink
    {
        public void Write(string relativePath, Action<Stream> contentWriter)
        {
            using var stream = new MemoryStream();
            contentWriter(stream);
        }

        public void WriteText(string relativePath, string content)
        {
        }
    }
}
