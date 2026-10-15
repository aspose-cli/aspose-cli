using Aspose.Words;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// set_style makes the blocks look like the style: the direct typeface, size and color of their
/// runs give way to it, while emphasis such as italic stays.
/// </summary>
public sealed class WordsSetStyleTests
{
    [Fact]
    public void SetStyle_Heading1_ClearsTheDirectFontAndSizeButKeepsEmphasis()
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Font.Name = "Courier New";
        builder.Font.Size = 9;
        builder.Font.Color = System.Drawing.Color.Green;
        builder.Write("Plain ");
        builder.Font.Italic = true;
        builder.Write("stressed");
        string input = fixture.Temp.File("direct.docx");
        source.Save(input, SaveFormat.Docx);
        string output = fixture.Temp.File("heading.docx");

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch { Ops = [new SetStyleOp { Target = new WordsTarget { Block = 1 }, Style = "Heading 1" }] },
            Output = TestOutput.At(output),
        });

        var edited = new Document(output);
        Style heading = edited.Styles[StyleIdentifier.Heading1];
        // Evaluation mode puts its banner before the first block.
        Paragraph styled = edited.FirstSection.Body.Paragraphs.Cast<Paragraph>().Single(static paragraph => paragraph.GetText().StartsWith("Plain", StringComparison.Ordinal));
        Run[] runs = [.. styled.Runs.Cast<Run>()];
        Assert.All(runs, run =>
        {
            Assert.Equal(heading.Font.Size, run.Font.Size);
            Assert.Equal(heading.Font.Name, run.Font.Name);
            Assert.Equal(heading.Font.Color.ToArgb(), run.Font.Color.ToArgb());
        });
        Assert.False(runs.Single(static run => run.Text == "Plain ").Font.Italic);
        Assert.True(runs.Single(static run => run.Text == "stressed").Font.Italic);
    }
    /// <summary>Only the typeface, size and color give way: direction, language and the character style of a run stay.</summary>
    [Fact]
    public void SetStyle_KeepsARunsDirectionLanguageAndCharacterStyle()
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Font.Size = 9;
        builder.Font.StyleIdentifier = StyleIdentifier.Strong;
        builder.Font.Bidi = true;
        builder.Font.LocaleId = 1036;
        builder.Font.LocaleIdBi = 1025;
        builder.Font.NoProofing = true;
        // Right-to-left text: a save keeps the direction of such a run only.
        builder.Write("مرحبا");
        string input = fixture.Temp.File("marked.docx");
        source.Save(input, SaveFormat.Docx);
        string output = fixture.Temp.File("marked-heading.docx");

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch { Ops = [new SetStyleOp { Target = new WordsTarget { Block = 1 }, Style = "Heading 1" }] },
            Output = TestOutput.At(output),
        });

        var edited = new Document(output);
        Run run = edited.FirstSection.Body.Paragraphs.Cast<Paragraph>()
            .Single(static paragraph => paragraph.GetText().StartsWith("م", StringComparison.Ordinal)).Runs[0];
        Assert.Equal(edited.Styles[StyleIdentifier.Heading1].Font.Size, run.Font.Size);
        Assert.True(run.Font.Bidi);
        Assert.Equal(1036, run.Font.LocaleId);
        Assert.Equal(1025, run.Font.LocaleIdBi);
        Assert.True(run.Font.NoProofing);
        Assert.Equal(StyleIdentifier.Strong, run.Font.StyleIdentifier);
    }
    /// <summary>The typeface, size and color are inherited again, not copied: a later change of the style reaches the runs.</summary>
    [Fact]
    public void SetStyle_ThenDefineStyle_TheRunsFollowTheStylesNewSize()
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Font.Size = 9;
        builder.Write("Sized");
        string input = fixture.Temp.File("sized.docx");
        source.Save(input, SaveFormat.Docx);
        string output = fixture.Temp.File("sized-heading.docx");

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops =
                [
                    new SetStyleOp { Target = new WordsTarget { Block = 1 }, Style = "Heading 1" },
                    new DefineStyleOp { Name = "Heading 1", FontSize = 30 },
                ],
            },
            Output = TestOutput.At(output),
        });

        Run run = new Document(output).FirstSection.Body.Paragraphs.Cast<Paragraph>()
            .Single(static paragraph => paragraph.GetText().StartsWith("Sized", StringComparison.Ordinal)).Runs[0];
        Assert.Equal(30, run.Font.Size);
    }
}


