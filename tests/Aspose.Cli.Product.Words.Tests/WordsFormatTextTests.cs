using Aspose.Words;
using Aspose.Words.Markup;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// format_text with match formats only the text it matches inside the target blocks; the rest of
/// the paragraph, and every setting the operation does not name, keep their format.
/// </summary>
public sealed class WordsFormatTextTests : IClassFixture<WordsFixture>
{
    private const string Clause = "Party A pays Party B, and Party B signs.";

    private readonly WordsFixture _fixture;

    public WordsFormatTextTests(WordsFixture fixture) => _fixture = fixture;

    [Fact]
    public void Match_HighlightsOnlyTheMatchedText()
    {
        (WordsEditResult result, WordsBlockData block) = FormatClause("""{"op":"format_text","target":{"block":1},"match":"party b","highlight":"yellow","bold":true}""");

        AssertChangedAndVerified(result);
        Assert.Equal(Clause, block.Text);
        Assert.Equal(["Party B", "Party B"], block.Runs!.Where(static run => run.Highlight == "yellow").Select(static run => run.Text));
        Assert.All(block.Runs!.Where(static run => run.Highlight == "yellow"), static run => Assert.True(run.Bold));
        Assert.All(block.Runs!.Where(static run => run.Highlight is null), static run => Assert.NotEqual(true, run.Bold));
        Assert.All(block.Runs!, static run => Assert.Equal(("Arial", 11d), (run.LatinFont, run.FontSize!.Value)));
    }

    [Fact]
    public void Regex_HighlightsOnlyTheMatchItSelects()
    {
        (WordsEditResult result, WordsBlockData block) = FormatClause("""{"op":"format_text","target":{"block":1},"match":"(?<=and )Party B","regex":true,"highlight":"yellow"}""");

        AssertChangedAndVerified(result);
        Assert.Equal(Clause, block.Text);
        WordsRunData highlighted = Assert.Single(block.Runs!, static run => run.Highlight is not null);
        Assert.Equal("Party B", highlighted.Text);
        Assert.EndsWith("and ", string.Concat(block.Runs!.TakeWhile(run => !ReferenceEquals(run, highlighted)).Select(static run => run.Text)), StringComparison.Ordinal);
    }

    [Fact]
    public void Match_WithoutAMatch_ChangesNothingAndSaysSo()
    {
        (WordsEditResult result, WordsBlockData block) = FormatClause("""{"op":"format_text","target":{"block":1},"match":"Party C","highlight":"yellow"}""");

        Assert.Equal(0L, Assert.Single(result.Applied).ItemsAffected);
        Warning warning = Assert.Single(result.Warnings!, static warning => warning.Code.Name == "OP_NO_EFFECT");
        Assert.Contains("matched no text", warning.Message, StringComparison.Ordinal);
        Assert.Equal(Clause, block.Text);
        Assert.All(block.Runs!, static run => Assert.Null(run.Highlight));
    }

    /// <summary>A match over runs of different formats keeps each run's own format beside the settings the operation names.</summary>
    [Fact]
    public void Match_OverRunsOfDifferentFormats_KeepsTheirOwnFormat()
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Font.Name = "Arial";
        builder.Write("Party A pays Pa");
        builder.Font.Italic = true;
        builder.Write("rty B");
        builder.Font.Italic = false;
        builder.Write(" on time.");

        (WordsEditResult result, WordsBlockData block) = Format(document, """{"op":"format_text","target":{"block":1},"match":"Party B","highlight":"yellow"}""");

        AssertChangedAndVerified(result);
        Assert.Equal("Party A pays Party B on time.", block.Text);
        Assert.Equal(
            ["Pa:False", "rty B:True"],
            block.Runs!.Where(static run => run.Highlight == "yellow").Select(static run => $"{run.Text}:{run.Italic == true}"));
    }

    /// <summary>In a document that tracks changes, formatting a match records no deletion or insertion of its text.</summary>
    [Fact]
    public void Match_InADocumentThatTracksChanges_RecordsNoTextRevision()
    {
        var document = new Document();
        new DocumentBuilder(document).Write(Clause);
        document.TrackRevisions = true;

        (WordsEditResult result, WordsBlockData block) = Format(document, """{"op":"format_text","target":{"block":1},"match":"Party B","bold":true}""");

        AssertChangedAndVerified(result);
        Assert.Equal(Clause, block.Text);
        Assert.DoesNotContain(new Document(result.Output!.Path).Revisions, static revision => revision.RevisionType is RevisionType.Insertion or RevisionType.Deletion);
    }

    /// <summary>A match over a hyperlink formats its display text, never the field code that holds the address.</summary>
    [Fact]
    public void Match_IntoAHyperlink_FormatsItsDisplayTextOnly()
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Write("Pay Party ");
        builder.InsertHyperlink("B", "https://example.com/", false);
        builder.Write(" now.");

        (WordsEditResult result, WordsBlockData block) = Format(document, """{"op":"format_text","target":{"block":1},"match":"Party B","highlight":"yellow"}""");

        AssertChangedAndVerified(result);
        Assert.Equal("Pay Party B now.", block.Text);
        Assert.Equal("Party B", HighlightedText(result));
    }

    /// <summary>A match into an inline content control formats the control's text, not the text after it.</summary>
    [Fact]
    public void Match_IntoAnInlineContentControl_FormatsItsTextOnly()
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Write("Pay Party ");
        var control = new StructuredDocumentTag(document, SdtType.PlainText, MarkupLevel.Inline);
        control.RemoveAllChildren();
        control.AppendChild(new Run(document, "B"));
        control.IsShowingPlaceholderText = false;
        builder.CurrentParagraph.AppendChild(control);
        builder.CurrentParagraph.AppendChild(new Run(document, " now."));

        (WordsEditResult result, WordsBlockData block) = Format(document, """{"op":"format_text","target":{"block":1},"match":"Party B","highlight":"yellow"}""");

        AssertChangedAndVerified(result);
        Assert.Equal("Pay Party B now.", block.Text);
        Assert.Equal("Party B", HighlightedText(result));
    }

    /// <summary>A match that reaches past its paragraph, and an empty match, format nothing and are not counted.</summary>
    [Theory]
    [InlineData("signs\\.\\r?\\n?Party")]
    [InlineData("signs\\.[\\s\\S]Party")]
    [InlineData("(?=Party)")]
    public void Match_ThatSpansParagraphsOrIsEmpty_FormatsNothing(string pattern)
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Writeln(Clause);
        builder.Write("Party C agrees.");

        (WordsEditResult result, _) = Format(document, $$"""{"op":"format_text","target":{"blocks":"1-2"},"match":{{System.Text.Json.JsonSerializer.Serialize(pattern)}},"regex":true,"highlight":"yellow"}""");

        Assert.Equal(0L, Assert.Single(result.Applied).ItemsAffected);
        Assert.Contains(result.Warnings!, static warning => warning.Code.Name == "OP_NO_EFFECT");
        Assert.Equal(string.Empty, HighlightedText(result));
    }

    /// <summary>A match in a text box anchored in the target paragraph is formatted, as words search finds it there.</summary>
    [Fact]
    public void Match_InATextBoxOfTheParagraph_IsFormattedAsSearchFindsIt()
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Write("Signatures: ");
        Aspose.Words.Drawing.Shape box = builder.InsertShape(Aspose.Words.Drawing.ShapeType.TextBox, 200, 50);
        builder.MoveTo(box.LastParagraph);
        builder.Write("Party B signs here.");
        string input = _fixture.Temp.File($"box-{Guid.NewGuid():N}.docx");
        document.Save(input);

        WordsSearchResult search = WordsSearch.Run(_fixture.Session, WordsFixture.Search(input, "Party B"));
        (WordsEditResult result, _) = Format(new Document(input), """{"op":"format_text","target":{"block":1},"match":"Party B","highlight":"yellow"}""");

        Assert.Equal(1, Assert.Single(search.Hits).Block);
        AssertChangedAndVerified(result);
        Assert.Equal("Party B", HighlightedText(result));
    }

    /// <summary>A pattern meets the end of the paragraph's text as words search reads it, without its paragraph mark.</summary>
    [Theory]
    [InlineData("Party B.*", "Party B signs.")]
    [InlineData("Party B$", "Party B")]
    public void Regex_ReachesTheEndOfTheParagraphText(string pattern, string formatted)
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Writeln("The buyer is Party B");
        builder.Write("Then Party B signs.");

        (WordsEditResult result, _) = Format(document, $$"""{"op":"format_text","target":{"block":{{(formatted == "Party B" ? 1 : 2)}}},"match":{{System.Text.Json.JsonSerializer.Serialize(pattern)}},"regex":true,"highlight":"yellow"}""");

        AssertChangedAndVerified(result);
        Assert.Equal(formatted, HighlightedText(result));
    }

    [Fact]
    public void CaseSensitive_FormatsOnlyAMatchOfTheSameCase()
    {
        (WordsEditResult result, _) = FormatClause("""{"op":"format_text","target":{"block":1},"match":"party b","caseSensitive":true,"highlight":"yellow"}""", "Party A pays Party B, and party b signs.");

        AssertChangedAndVerified(result);
        Assert.Equal("party b", HighlightedText(result));
    }

    [Fact]
    public void WholeWord_FormatsOnlyAMatchThatIsAWholeWord()
    {
        (WordsEditResult result, _) = FormatClause("""{"op":"format_text","target":{"block":1},"match":"Party B","wholeWord":true,"highlight":"yellow"}""", "Party BC pays Party B.");

        AssertChangedAndVerified(result);
        Assert.Equal("Party B", HighlightedText(result));
    }

    // The text of every highlighted run of the output, field codes included, in document order.
    private static string HighlightedText(WordsEditResult result) =>
        string.Concat(new Document(result.Output!.Path).GetChildNodes(NodeType.Run, true).Cast<Run>()
            .Where(static run => !run.Font.HighlightColor.IsEmpty && run.Font.HighlightColor.A != 0)
            .Select(static run => run.Text));

    private static void AssertChangedAndVerified(WordsEditResult result)
    {
        Assert.True(result.Verification!.Ok);
        Assert.Equal(1L, Assert.Single(result.Applied).ItemsAffected);
    }

    private (WordsEditResult Result, WordsBlockData Block) FormatClause(string operation, string clause = Clause)
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Font.Name = "Arial";
        builder.Font.Size = 11;
        builder.Write(clause);
        return Format(document, operation);
    }

    private (WordsEditResult Result, WordsBlockData Block) Format(Document document, string operation)
    {
        string input = _fixture.Temp.File($"clause-{Guid.NewGuid():N}.docx");
        document.Save(input);
        string output = _fixture.Temp.File($"clause-{Guid.NewGuid():N}.out.docx");

        WordsEditResult result = WordsEdit.Run(_fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = WordsOp.Catalog.Parse<WordsOpsBatch>($$"""{"ops":[{{operation}}]}""", Aspose.Cli.Generated.ProductJsonContext.Definition),
            Output = TestOutput.At(output),
            Verify = true,
        });

        return (result, WordsRead.Run(_fixture.Session, new WordsReadRequest { Input = output, Content = "full" }).Blocks[0]);
    }
}
