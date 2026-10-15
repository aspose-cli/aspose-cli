using Aspose.Slides;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// The replace_text options Slides shares with Words: wholeWord and maxReplacementCount. Each
/// paragraph stays at most five characters long, which evaluation mode reads whole.
/// </summary>
public sealed class SlidesReplaceTextTests
{
    [Theory]
    [InlineData(false, false, "X Xb")]
    [InlineData(true, false, "X ab")]
    [InlineData(true, true, "X ab")]
    public void WholeWord_ReplacesOnlyMatchesThatAreWholeWords(bool wholeWord, bool regex, string expected)
    {
        using var fixture = new SlidesEngineFixture();
        string input = Deck(fixture, "a ab");
        string output = fixture.File("whole-word.pptx");

        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops = [new SlidesReplaceTextOp
                {
                    Find = "a",
                    Replace = "X",
                    Regex = regex,
                    WholeWord = wholeWord,
                    Scope = "shapes",
                }],
            },
            Output = TestOutput.At(output),
        });

        Assert.Equal("ok", Assert.Single(result.Applied).Status);
        Assert.Equal([expected], Paragraphs(output));
    }

    [Fact]
    public void MaxReplacementCount_ReplacesTheFirstMatchesInDocumentOrder()
    {
        using var fixture = new SlidesEngineFixture();
        string input = Deck(fixture, "a a", "a");
        string output = fixture.File("max-count.pptx");

        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops = [new SlidesReplaceTextOp { Find = "a", Replace = "b", Scope = "shapes", MaxReplacementCount = 2 }],
            },
            Output = TestOutput.At(output),
        });

        Assert.Equal("ok", Assert.Single(result.Applied).Status);
        Assert.Equal(["b b", "a"], Paragraphs(output));
    }

    /// <summary>A one-slide deck whose one text box holds <paramref name="paragraphs"/>.</summary>
    private static string Deck(SlidesEngineFixture fixture, params string[] paragraphs)
    {
        string path = fixture.File("replace.pptx");
        using var presentation = new Presentation();
        IAutoShape box = presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 40, 40, 400, 200);
        box.Name = "Box";
        box.TextFrame.Paragraphs.Clear();
        foreach (string text in paragraphs)
        {
            box.TextFrame.Paragraphs.Add(new Paragraph { Text = text });
        }

        presentation.Save(path, Aspose.Slides.Export.SaveFormat.Pptx);
        return path;
    }

    private static string[] Paragraphs(string path)
    {
        using var presentation = new Presentation(path);
        IAutoShape box = presentation.Slides[0].Shapes.OfType<IAutoShape>().Single(static shape => shape.Name == "Box");
        return [.. box.TextFrame.Paragraphs.Select(static paragraph => paragraph.Text)];
    }
}
