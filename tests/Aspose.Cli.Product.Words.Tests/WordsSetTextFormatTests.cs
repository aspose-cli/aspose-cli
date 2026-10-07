using System.Drawing;
using Aspose.Words;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// set_text replaces a paragraph's text as typing over it in Word does: the new text keeps the
/// font of the text it replaces and the paragraph keeps its format.
/// </summary>
public sealed class WordsSetTextFormatTests
{
    private const string NewText = "The supplier pays a penalty for each day of delay.";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SetText_KeepsTheFontOfTheReplacedText(bool trackChanges)
    {
        using var fixture = new WordsFixture();
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.ParagraphFormat.FirstLineIndent = 24;
        builder.Font.Name = "SimSun";
        builder.Font.Size = 12;
        builder.Font.Color = Color.FromArgb(0x1F, 0x29, 0x37);
        builder.Write("Both parties agree on liability ");
        builder.Font.Bold = true;
        builder.Writeln("as follows.");
        string input = fixture.Temp.File("plain.docx");
        document.Save(input);

        Run run = SetText(fixture, input, new WordsEditRequest
        {
            Output = TestOutput.At(fixture.Temp.File("plain.out.docx")),
            TrackChanges = trackChanges,
            Author = trackChanges ? "Reviewer" : null,
        });

        AssertFont(run, "SimSun", 12);
        Assert.False(run.Font.Bold);
        Assert.Equal(24, run.ParentParagraph.ParagraphFormat.FirstLineIndent);
    }

    [Fact]
    public void SetText_InAParagraphConvertedFromPdf_KeepsItsFont()
    {
        using var fixture = new WordsFixture();
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Font.Name = "Arial";
        builder.Font.Size = 11;
        builder.Font.Color = Color.FromArgb(0x1F, 0x29, 0x37);
        builder.Writeln("Both parties agree on liability as follows.");
        builder.Writeln("Disputes go to the court of the buyer.");
        string input = fixture.Temp.File("source.pdf");
        document.Save(input, SaveFormat.Pdf);

        Run run = SetText(fixture, input, new WordsEditRequest { Output = TestOutput.At(fixture.Temp.File("from-pdf.docx")) });

        AssertFont(run, "Arial", 11);
    }

    private static Run SetText(WordsFixture fixture, string input, WordsEditRequest request)
    {
        fixture.Engine.ApplyOps(
            input,
            new WordsOpsBatch { Ops = [new SetTextOp { At = new WordsTarget { Find = "liability" }, Text = NewText }] },
            request);
        return new Document(request.Output.Path).GetChildNodes(NodeType.Run, true).Cast<Run>()
            .Single(static run => run.Text == NewText);
    }

    private static void AssertFont(Run run, string name, double size)
    {
        Assert.Equal(name, run.Font.Name);
        Assert.Equal(size, run.Font.Size);
        Assert.Equal(0x1F2937, run.Font.Color.ToArgb() & 0xFFFFFF);
    }
}
