using System.Drawing;
using System.Text.Json.Nodes;
using Aspose.Words;
using Aspose.Words.Fields;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// set_page_numbers with text writes the number and its words as one paragraph: {n} is a PAGE
/// field, {N} a NUMPAGES field and everything else the text as given, and inspect reads it back
/// as pageNumbers.text, under the same name and meaning as the PDF add_page_numbers text.
/// </summary>
public sealed class WordsPageNumberTextTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    /// <summary>The input of each case holds either the built-in design's lone page number or no footer at all.</summary>
    [Theory]
    [InlineData("built-in", "第 {n} 页", "第 {PAGE} 页", new[] { FieldType.FieldPage })]
    [InlineData("built-in", "第 {n} 页，共 {N} 页", "第 {PAGE} 页，共 {NUMPAGES} 页", new[] { FieldType.FieldPage, FieldType.FieldNumPages })]
    [InlineData("plain", "第 {n} 页", "第 {PAGE} 页", new[] { FieldType.FieldPage })]
    public void Text_WritesTheNumberAndItsWordsAsOneParagraph(string input, string text, string footer, FieldType[] fields)
    {
        CreateInput(input);

        CliResult edited = Edit($$"""{"ops":[{"op":"set_page_numbers","text":"{{text}}"}]}""", "--out", "numbered.docx");

        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
        Assert.Equal([footer], FooterLines("numbered.docx"));
        JsonNode pageNumbers = Inspected("numbered.docx")["sections"]![0]!["pageNumbers"]!;
        Assert.Equal(text, pageNumbers["text"]!.GetValue<string>());
        Assert.Equal("footer", pageNumbers["location"]!.GetValue<string>());

        HeaderFooter story = new Document(_workspace.File("numbered.docx")).FirstSection.HeadersFooters[HeaderFooterType.FooterPrimary];
        Field[] written = [.. story.Range.Fields.Cast<Field>()];
        Assert.Equal(fields, written.Select(static field => field.Type));
        Assert.Single(written.Select(static field => field.Start.GetAncestor(NodeType.Paragraph)).Distinct());
    }

    /// <summary>The words take the font of the paragraph's number and the op's alignment.</summary>
    [Fact]
    public void Text_TakesTheFontOfWhatItReplaces()
    {
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Write("Body text.");
        builder.MoveToHeaderFooter(HeaderFooterType.FooterPrimary);
        builder.Font.Name = "Arial";
        builder.Font.Size = 9;
        builder.Font.Color = Color.Navy;
        builder.Write("Page ");
        builder.InsertField("PAGE");
        source.Save(_workspace.File("input.docx"), SaveFormat.Docx);

        CliResult edited = Edit("""{"ops":[{"op":"set_page_numbers","alignment":"right","text":"第 {n} 页"}]}""", "--out", "numbered.docx");

        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
        Paragraph paragraph = PageParagraph(new Document(_workspace.File("numbered.docx")));
        Assert.Equal(ParagraphAlignment.Right, paragraph.ParagraphFormat.Alignment);
        Assert.All(paragraph.GetChildNodes(NodeType.Run, true).Cast<Run>().Where(static run => run.GetAncestor(NodeType.Paragraph) is not null), run =>
        {
            Assert.Equal("Arial", run.Font.Name);
            Assert.Equal(9, run.Font.Size);
            Assert.Equal(Color.Navy.ToArgb(), run.Font.Color.ToArgb());
        });
    }

    /// <summary>The text replaces only the paragraph that holds the number; the footer's other paragraphs stay.</summary>
    [Fact]
    public void Text_ReplacesOnlyTheParagraphThatHoldsTheNumber()
    {
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Write("Body text.");
        builder.MoveToHeaderFooter(HeaderFooterType.FooterPrimary);
        builder.Writeln("Confidential");
        builder.Write("Page ");
        builder.InsertField("PAGE");
        source.Save(_workspace.File("input.docx"), SaveFormat.Docx);

        CliResult edited = Edit("""{"ops":[{"op":"set_page_numbers","text":"第 {n} 页"}]}""", "--out", "numbered.docx");

        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
        Assert.Equal(["Confidential", "第 {PAGE} 页"], FooterLines("numbered.docx"));
    }

    /// <summary>
    /// Like the PDF add_page_numbers text, a text without {n} is written as it is, without an
    /// error: here it goes in a paragraph of its own and the number stays where it was.
    /// </summary>
    [Fact]
    public void Text_WithoutThePageNumber_IsWrittenAsItIsAndLeavesTheNumberAlone()
    {
        CreateInput("built-in");

        CliResult edited = Edit("""{"ops":[{"op":"set_page_numbers","text":"Draft"}]}""", "--out", "drafted.docx");

        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
        Assert.Equal(["{PAGE}", "Draft"], FooterLines("drafted.docx"));
        Assert.Equal("footer", Inspected("drafted.docx")["sections"]![0]!["pageNumbers"]!["location"]!.GetValue<string>());
    }

    /// <summary>Moved to the header with its words, the number leaves the footer.</summary>
    [Fact]
    public void Text_InTheOtherLocation_MovesTheNumberThere()
    {
        CreateInput("built-in");

        CliResult edited = Edit("""{"ops":[{"op":"set_page_numbers","location":"header","text":"第 {n} 页"}]}""", "--out", "moved.docx");

        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
        JsonNode section = Inspected("moved.docx")["sections"]![0]!;
        Assert.Equal("header", section["pageNumbers"]!["location"]!.GetValue<string>());
        Assert.Equal("第 {n} 页", section["pageNumbers"]!["text"]!.GetValue<string>());
        HeaderFooter footer = new Document(_workspace.File("moved.docx")).FirstSection.HeadersFooters[HeaderFooterType.FooterPrimary];
        Assert.DoesNotContain(footer.Range.Fields.Cast<Field>(), static field => field.Type == FieldType.FieldPage);
    }

    /// <summary>Without text the number stands alone and inspect reports no text, as before text existed.</summary>
    [Fact]
    public void WithoutText_TheNumberStandsAlone()
    {
        CreateInput("plain");

        CliResult edited = Edit("""{"ops":[{"op":"set_page_numbers"}]}""", "--out", "numbered.docx");

        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
        Assert.Equal(["{PAGE}"], FooterLines("numbered.docx"));
        Assert.False(Inspected("numbered.docx")["sections"]![0]!["pageNumbers"]!.AsObject().ContainsKey("text"));
    }

    /// <summary>Without text, a number that exists keeps its paragraph and its words as they are.</summary>
    [Fact]
    public void WithoutText_AnExistingNumberKeepsItsParagraph()
    {
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Write("Body text.");
        builder.MoveToHeaderFooter(HeaderFooterType.FooterPrimary);
        builder.Write("Page ");
        builder.InsertField("PAGE");
        source.Save(_workspace.File("input.docx"), SaveFormat.Docx);

        CliResult edited = Edit("""{"ops":[{"op":"set_page_numbers"}]}""", "--out", "numbered.docx");

        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
        Assert.Equal(["Page {PAGE}"], FooterLines("numbered.docx"));
    }

    /// <summary>
    /// Without text, moving the number takes its words along, as a text of the same words would:
    /// a footer numbered with a text, and one another tool wrote. The old location keeps none of them.
    /// </summary>
    [Theory]
    [InlineData("text", "第 {PAGE} 页", "第 {n} 页")]
    [InlineData("page-of-pages", "Page {PAGE} of {NUMPAGES}", "Page {n} of {N}")]
    public void WithoutText_MovingTheNumberTakesItsWordsAlong(string input, string header, string text)
    {
        if (input == "text")
        {
            CreateInput("built-in");
            CliResult numbered = Edit("""{"ops":[{"op":"set_page_numbers","text":"第 {n} 页"}]}""", "--out", "numbered.docx");
            Assert.True(numbered.ExitCode == 0, numbered.StdErr + numbered.StdOut);
            File.Copy(_workspace.File("numbered.docx"), _workspace.File("input.docx"), overwrite: true);
        }
        else
        {
            CreateFooter(static builder =>
            {
                builder.Write("Page ");
                builder.InsertField("PAGE");
                builder.Write(" of ");
                builder.InsertField("NUMPAGES");
            });
        }

        CliResult edited = Edit("""{"ops":[{"op":"set_page_numbers","location":"header"}]}""", "--out", "moved.docx", "--verify");

        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
        Assert.True(JsonNode.Parse(edited.StdOut)!["verification"]!["ok"]!.GetValue<bool>());
        Assert.Contains(header, Lines("moved.docx", "header"));
        Assert.DoesNotContain(FooterLines("moved.docx"), static line => line.Length > 0);
        Assert.Empty(new Document(_workspace.File("moved.docx")).FirstSection.HeadersFooters[HeaderFooterType.FooterPrimary].Range.Fields.Cast<Field>());
        JsonNode pageNumbers = Inspected("moved.docx")["sections"]![0]!["pageNumbers"]!;
        Assert.Equal("header", pageNumbers["location"]!.GetValue<string>());
        Assert.Equal(text, pageNumbers["text"]!.GetValue<string>());
    }

    /// <summary>
    /// Without text, a section that shows an earlier section's footer moves the same words to its
    /// own header. Licensed only: evaluation mode gives every section a header and footer of its own.
    /// </summary>
    [LicensedFact]
    public void WithoutText_MovingTheNumberASectionContinues_TakesItsWordsToEverySection()
    {
        TestLicense.Apply(WordsActivation.ApplyLicense);
        string license = TestLicense.Path!;
        CreateFooter(static builder =>
        {
            builder.Write("Page ");
            builder.InsertField("PAGE");
            builder.Write(" of ");
            builder.InsertField("NUMPAGES");
            builder.MoveToDocumentEnd();
            builder.InsertBreak(BreakType.SectionBreakNewPage);
            builder.CurrentSection.HeadersFooters.LinkToPrevious(true);
            builder.Write("Second section.");
        });
        Assert.Null(new Document(_workspace.File("input.docx")).LastSection.HeadersFooters[HeaderFooterType.FooterPrimary]);

        CliResult edited = Edit("""{"ops":[{"op":"set_page_numbers","location":"header"}]}""", "--out", "moved.docx", "--license", license);

        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
        CliResult inspected = _workspace.Run("words", "inspect", "moved.docx", "--detail", "sections", "--license", license, "--output-format", "json");
        Assert.True(inspected.ExitCode == 0, inspected.StdErr + inspected.StdOut);
        JsonNode[] sections = [.. JsonNode.Parse(inspected.StdOut)!["sections"]!.AsArray().Select(static section => section!)];
        Assert.Equal(2, sections.Length);
        Assert.All(sections, static section =>
        {
            Assert.Equal("header", section["pageNumbers"]!["location"]!.GetValue<string>());
            Assert.Equal("Page {n} of {N}", section["pageNumbers"]!["text"]!.GetValue<string>());
        });
        Assert.Empty(new Document(_workspace.File("moved.docx")).FirstSection.HeadersFooters[HeaderFooterType.FooterPrimary].Range.Fields.Cast<Field>());
    }

    /// <summary>Without text, a number alone moves as it always has: its field goes, and no words.</summary>
    [Fact]
    public void WithoutText_MovingALoneNumberMovesItsField()
    {
        CreateInput("built-in");

        CliResult edited = Edit("""{"ops":[{"op":"set_page_numbers","location":"header"}]}""", "--out", "moved.docx");

        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
        Assert.Contains("{PAGE}", Lines("moved.docx", "header"));
        Assert.DoesNotContain(FooterLines("moved.docx"), static line => line.Length > 0);
        Assert.False(Inspected("moved.docx")["sections"]![0]!["pageNumbers"]!.AsObject().ContainsKey("text"));
    }

    /// <summary>Without text, a number alone beside an image moves without it: the image stays where it was.</summary>
    [Fact]
    public void WithoutText_MovingALoneNumberBesideAnImageLeavesTheImage()
    {
        CreateFooter(static builder =>
        {
            builder.InsertField("PAGE");
            builder.InsertImage(Pixel);
        });

        CliResult edited = Edit("""{"ops":[{"op":"set_page_numbers","location":"header"}]}""", "--out", "moved.docx");

        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
        HeaderFooter footer = new Document(_workspace.File("moved.docx")).FirstSection.HeadersFooters[HeaderFooterType.FooterPrimary];
        Assert.Empty(footer.Range.Fields.Cast<Field>());
        Assert.Single(footer.GetChildNodes(NodeType.Shape, true).Cast<Aspose.Words.Drawing.Shape>(), static shape => shape.HasImage);
        Assert.Contains("{PAGE}", Lines("moved.docx", "header"));
    }

    /// <summary>
    /// Without text, a number moved where one already is keeps the words written there, and the
    /// location it leaves keeps none of its own.
    /// </summary>
    [Fact]
    public void WithoutText_MovingTheNumberWhereOneIs_KeepsThatOneAndClearsTheOther()
    {
        CreateFooter(static builder =>
        {
            builder.Write("Page ");
            builder.InsertField("PAGE");
            builder.Write(" of ");
            builder.InsertField("NUMPAGES");
            builder.MoveToHeaderFooter(HeaderFooterType.HeaderPrimary);
            builder.Write("第 ");
            builder.InsertField("PAGE");
            builder.Write(" 页");
        });

        CliResult edited = Edit("""{"ops":[{"op":"set_page_numbers","location":"header"}]}""", "--out", "moved.docx");

        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
        Assert.Equal(["第 {PAGE} 页"], Lines("moved.docx", "header"));
        Assert.DoesNotContain(FooterLines("moved.docx"), static line => line.Length > 0);
        Assert.Empty(new Document(_workspace.File("moved.docx")).FirstSection.HeadersFooters[HeaderFooterType.FooterPrimary].Range.Fields.Cast<Field>());
    }

    /// <summary>
    /// Without text, words that stand beside an image, which a text cannot carry, are refused
    /// rather than left behind or lost, and nothing is written.
    /// </summary>
    [Fact]
    public void WithoutText_MovingTheNumberFromAParagraphWithAnImage_IsRefused()
    {
        CreateFooter(static builder =>
        {
            builder.Write("Page ");
            builder.InsertField("PAGE");
            builder.InsertImage(Pixel);
        });
        byte[] before = File.ReadAllBytes(_workspace.File("input.docx"));

        CliResult edited = Edit("""{"ops":[{"op":"set_page_numbers","location":"header"}]}""", "--out", "moved.docx");

        AssertRefused(edited, "an image", "set_footer");
        Assert.DoesNotContain("omit text", JsonNode.Parse(edited.StdErr)!["error"]!["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(File.Exists(_workspace.File("moved.docx")));
        Assert.Equal(before, File.ReadAllBytes(_workspace.File("input.docx")));
    }

    /// <summary>
    /// Without text, words that stand beside a field other than the page number and count are
    /// refused, naming the field, and nothing is written. The hint's two ways out each work: a
    /// text writes the words anew and drops the field with the paragraph it leaves; rewriting the
    /// footer first leaves a number with no words to move.
    /// </summary>
    [Theory]
    [InlineData("DATE")]
    [InlineData("SECTIONPAGES")]
    public void WithoutText_MovingTheNumberBesideAnotherField_IsRefusedWithBothWaysOut(string other)
    {
        CreateFooter(builder =>
        {
            builder.Write("第 ");
            builder.InsertField("PAGE");
            builder.Write(" 页 共 ");
            builder.InsertField("NUMPAGES");
            builder.Write(" 页    ");
            builder.InsertField(other);
        });
        byte[] before = File.ReadAllBytes(_workspace.File("input.docx"));

        CliResult refused = Edit("""{"ops":[{"op":"set_page_numbers","location":"header"}]}""", "--out", "moved.docx");

        AssertRefused(refused, $"a {other} field", "Give text", $"drops its {other} field", "rewrite the footer with set_footer first");
        Assert.DoesNotContain("image", JsonNode.Parse(refused.StdErr)!["error"]!["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(File.Exists(_workspace.File("moved.docx")));
        Assert.Equal(before, File.ReadAllBytes(_workspace.File("input.docx")));

        CliResult withText = Edit("""{"ops":[{"op":"set_page_numbers","location":"header","text":"第 {n} 页 共 {N} 页"}]}""", "--out", "text.docx", "--verify");
        Assert.True(withText.ExitCode == 0, withText.StdErr + withText.StdOut);
        Assert.Contains("第 {PAGE} 页 共 {NUMPAGES} 页", Lines("text.docx", "header"));
        Assert.Empty(new Document(_workspace.File("text.docx")).FirstSection.HeadersFooters[HeaderFooterType.FooterPrimary].Range.Fields.Cast<Field>());

        CliResult rewritten = Edit(
            """{"ops":[{"op":"set_footer","paragraphs":[{"text":"Printed copy"}]},{"op":"set_page_numbers","location":"header"}]}""",
            "--out", "rewritten.docx", "--verify");
        Assert.True(rewritten.ExitCode == 0, rewritten.StdErr + rewritten.StdOut);
        Assert.Contains("{PAGE}", Lines("rewritten.docx", "header"));
        Assert.Equal(["Printed copy"], FooterLines("rewritten.docx"));
    }

    /// <summary>
    /// Without text, a number moved where one already is takes no words along, so the paragraph
    /// it leaves is cleared even when it holds another field, as with text, rather than refused.
    /// </summary>
    [Fact]
    public void WithoutText_MovingTheNumberWhereOneIs_ClearsAParagraphWithAnotherField()
    {
        CreateFooter(static builder =>
        {
            builder.Write("Page ");
            builder.InsertField("PAGE");
            builder.Write(" printed ");
            builder.InsertField("DATE");
            builder.MoveToHeaderFooter(HeaderFooterType.HeaderPrimary);
            builder.Write("第 ");
            builder.InsertField("PAGE");
            builder.Write(" 页");
        });

        CliResult edited = Edit("""{"ops":[{"op":"set_page_numbers","location":"header"}]}""", "--out", "moved.docx", "--verify");

        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
        Assert.Equal(["第 {PAGE} 页"], Lines("moved.docx", "header"));
        Assert.DoesNotContain(FooterLines("moved.docx"), static line => line.Length > 0);
        Assert.Empty(new Document(_workspace.File("moved.docx")).FirstSection.HeadersFooters[HeaderFooterType.FooterPrimary].Range.Fields.Cast<Field>());
    }

    /// <summary>
    /// --verify reads back the words the batch's last set_page_numbers moved with the number: an
    /// output whose page numbers do not read so, as when the words stayed behind, is a
    /// PAGE_NUMBERS_MISMATCH issue at the section; one that reads so is none, and words an earlier
    /// operation moved are not checked.
    /// </summary>
    [Fact]
    public void Verify_ReadsBackTheWordsTheNumberMoved()
    {
        var moves = new Aspose.Cli.Product.Words.Engine.Editing.WordsPageNumberLosses();
        moves.Moved(new SetPageNumbersOp { Id = "op-0002", Location = "header" }, 0, "第 {n} 页");
        static Document Output(bool moved)
        {
            var document = new Document();
            var builder = new DocumentBuilder(document);
            builder.MoveToHeaderFooter(HeaderFooterType.HeaderPrimary);
            builder.Write(moved ? "第 " : string.Empty);
            builder.InsertField("PAGE");
            builder.Write(moved ? " 页" : string.Empty);
            builder.MoveToHeaderFooter(HeaderFooterType.FooterPrimary);
            builder.Write(moved ? string.Empty : "第  页");
            return document;
        }

        var issue = Assert.Single(moves.Unmoved(Output(moved: false), "op-0002"));

        Assert.Equal("PAGE_NUMBERS_MISMATCH", issue.Code);
        Assert.Equal("section/1", issue.Location);
        Assert.Contains("第 {n} 页", issue.Message, StringComparison.Ordinal);
        Assert.Empty(moves.Unmoved(Output(moved: true), "op-0002"));
        Assert.Empty(moves.Unmoved(Output(moved: false), "op-0003"));
    }

    /// <summary>A text without {n} in the other location moves nothing: it takes a paragraph there and the number stays.</summary>
    [Fact]
    public void Text_WithoutThePageNumberInTheOtherLocation_MovesNothing()
    {
        CreateInput("built-in");

        CliResult edited = Edit("""{"ops":[{"op":"set_page_numbers","location":"header","text":"Draft"}]}""", "--out", "drafted.docx");

        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
        Assert.Contains("Draft", Lines("drafted.docx", "header"));
        Assert.Equal(["{PAGE}"], FooterLines("drafted.docx"));
        Assert.Equal("footer", Inspected("drafted.docx")["sections"]![0]!["pageNumbers"]!["location"]!.GetValue<string>());
    }

    /// <summary>A text is its paragraph's whole content, so a paragraph that also holds an image refuses it and the document stays as it was.</summary>
    [Theory]
    [InlineData("footer", "set_footer")]
    [InlineData("header", "set_header")]
    public void Text_OverAParagraphWithAnImage_IsRefused(string location, string rewrite)
    {
        CreateNumberedParagraphWithAnImage(location == "header" ? HeaderFooterType.HeaderPrimary : HeaderFooterType.FooterPrimary);
        byte[] before = File.ReadAllBytes(_workspace.File("input.docx"));

        CliResult edited = Edit($$"""{"ops":[{"op":"set_page_numbers","location":"{{location}}","text":"第 {n} 页"}]}""", "--out", "numbered.docx");

        AssertRefused(edited, "image", rewrite);
        Assert.False(File.Exists(_workspace.File("numbered.docx")));
        Assert.Equal(before, File.ReadAllBytes(_workspace.File("input.docx")));
    }

    /// <summary>With text, the paragraph the number leaves is cleared: no words and no NUMPAGES field stay in the old location.</summary>
    [Fact]
    public void Text_MovingTheNumber_LeavesNothingOfItsParagraphBehind()
    {
        CreateInput("built-in");
        CliResult numbered = Edit("""{"ops":[{"op":"set_page_numbers","text":"第 {n} 页，共 {N} 页"}]}""", "--out", "numbered.docx");
        Assert.True(numbered.ExitCode == 0, numbered.StdErr + numbered.StdOut);

        CliResult moved = _workspace.RunWithInput("""{"ops":[{"op":"set_page_numbers","location":"header","text":"第 {n} 页，共 {N} 页"}]}""",
            "words", "edit", "numbered.docx", "--ops", "-", "--out", "moved.docx", "--output-format", "json");

        Assert.True(moved.ExitCode == 0, moved.StdErr + moved.StdOut);
        Assert.DoesNotContain(FooterLines("moved.docx"), static line => line.Length > 0);
        Section section = new Document(_workspace.File("moved.docx")).FirstSection;
        Assert.Empty(section.HeadersFooters[HeaderFooterType.FooterPrimary].Range.Fields.Cast<Field>());
        Assert.Equal(
            [FieldType.FieldPage, FieldType.FieldNumPages],
            section.HeadersFooters[HeaderFooterType.HeaderPrimary].Range.Fields.Cast<Field>().Select(static field => field.Type));
        Assert.Equal("第 {n} 页，共 {N} 页", Inspected("moved.docx")["sections"]![0]!["pageNumbers"]!["text"]!.GetValue<string>());
    }

    /// <summary>The paragraph the number would leave holds an image: the move is refused like a replacement, and no output is written.</summary>
    [Fact]
    public void Text_MovingTheNumberFromAParagraphWithAnImage_IsRefused()
    {
        CreateNumberedParagraphWithAnImage(HeaderFooterType.FooterPrimary);

        CliResult edited = Edit("""{"ops":[{"op":"set_page_numbers","location":"header","text":"第 {n} 页"}]}""", "--out", "moved.docx");

        AssertRefused(edited, "image", "set_footer");
        Assert.DoesNotContain("omit text", JsonNode.Parse(edited.StdErr)!["error"]!["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(File.Exists(_workspace.File("moved.docx")));
    }

    /// <summary>{PAGE}, {NUMPAGES} and {SECTIONPAGES} are how a read shows the fields; written in a text they would be literal words, so they point to {n} and {N}.</summary>
    [Theory]
    [InlineData("第 {PAGE} 页")]
    [InlineData("共 {NUMPAGES} 页")]
    [InlineData("本节共 {SECTIONPAGES} 页")]
    public void Text_WithAFieldWrittenAsAReadShowsIt_IsRefused(string text)
    {
        CreateInput("built-in");

        CliResult edited = Edit($$"""{"ops":[{"op":"set_page_numbers","text":"{{text}}"}]}""", "--out", "numbered.docx");

        AssertRefused(edited, "literal", "{n}", "{N}");
    }

    /// <summary>On a document of several pages, the PDF of the edited document shows each page's number and the real page count.</summary>
    [Fact]
    public void Text_RendersEachPagesNumberAndTheDocumentsPageCount()
    {
        File.WriteAllText(_workspace.File("source.md"), string.Join("\n\n", Enumerable.Range(1, 90).Select(static number => $"Body paragraph {number}.")));
        Assert.Equal(0, _workspace.Run("words", "create", "input.docx", "--from-markdown", "source.md").ExitCode);
        CliResult edited = Edit("""{"ops":[{"op":"set_page_numbers","text":"Page {n} of {N}"}]}""", "--out", "numbered.docx");
        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);

        CliResult converted = _workspace.Run("words", "convert", "numbered.docx", "--to", "pdf", "--out", "numbered.pdf", "--output-format", "json");

        Assert.True(converted.ExitCode == 0, converted.StdErr + converted.StdOut);
        CliResult inspected = _workspace.Run("pdf", "inspect", "numbered.pdf", "--output-format", "json");
        Assert.True(inspected.ExitCode == 0, inspected.StdErr + inspected.StdOut);
        int pages = JsonNode.Parse(inspected.StdOut)!["pdf"]!["pageCount"]!.GetValue<int>();
        Assert.InRange(pages, 2, int.MaxValue);
        foreach (int page in new[] { 1, 2 })
        {
            CliResult read = _workspace.Run("pdf", "read", "numbered.pdf", "--pages", page.ToString(System.Globalization.CultureInfo.InvariantCulture), "--content", "text", "--output-format", "json");
            Assert.True(read.ExitCode == 0, read.StdErr + read.StdOut);
            Assert.Contains($"Page {page} of {pages}", read.StdOut, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// With --verify, a batch that replaces a numbered footer and numbers the pages again with
    /// text exits 0 without PAGE_NUMBERS_REMOVED.
    /// </summary>
    [Fact]
    public void Verify_ReplacingANumberedFooterAndNumberingItAgainWithText_ReportsNothingRemoved()
    {
        CreateInput("built-in");

        CliResult edited = Edit(
            """{"ops":[{"op":"set_footer","paragraphs":[{"text":"Confidential"}]},{"op":"set_page_numbers","text":"第 {n} 页"}]}""",
            "--out", "numbered.docx", "--verify");

        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
        JsonNode result = JsonNode.Parse(edited.StdOut)!;
        Assert.DoesNotContain(result["warnings"]?.AsArray() ?? [], static item => item!["code"]!.GetValue<string>() == "PAGE_NUMBERS_REMOVED");
        Assert.True(result["verification"]!["ok"]!.GetValue<bool>());
        Assert.Equal(["Confidential", "第 {PAGE} 页"], FooterLines("numbered.docx"));
    }

    /// <summary>A footer numbered with text counts as numbered: replacing it with --verify reports PAGE_NUMBERS_REMOVED and exits 8.</summary>
    [Fact]
    public void Verify_ReplacingAFooterNumberedWithText_ReportsTheNumbersRemoved()
    {
        CreateInput("built-in");
        CliResult numbered = Edit("""{"ops":[{"op":"set_page_numbers","text":"第 {n} 页"}]}""", "--out", "numbered.docx");
        Assert.True(numbered.ExitCode == 0, numbered.StdErr + numbered.StdOut);

        CliResult replaced = _workspace.RunWithInput("""{"ops":[{"op":"set_footer","paragraphs":[{"text":"Confidential"}]}]}""",
            "words", "edit", "numbered.docx", "--ops", "-", "--out", "replaced.docx", "--verify", "--output-format", "json");

        Assert.True(replaced.ExitCode == 8, replaced.StdErr + replaced.StdOut);
        JsonNode result = JsonNode.Parse(replaced.StdOut)!;
        Assert.Contains(result["warnings"]!.AsArray(), static item => item!["code"]!.GetValue<string>() == "PAGE_NUMBERS_REMOVED");
        Assert.False(result["verification"]!["ok"]!.GetValue<bool>());
    }

    private void CreateInput(string kind)
    {
        if (kind == "built-in")
        {
            File.WriteAllText(_workspace.File("source.md"), "# Contract\n\nOriginal clause.\n");
            Assert.Equal(0, _workspace.Run("words", "create", "input.docx", "--from-markdown", "source.md").ExitCode);
            return;
        }

        var source = new Document();
        new DocumentBuilder(source).Write("Body text.");
        source.Save(_workspace.File("input.docx"), SaveFormat.Docx);
    }

    /// <summary>An input whose primary header or footer paragraph holds words, the PAGE field and an inline image.</summary>
    private void CreateNumberedParagraphWithAnImage(HeaderFooterType type)
    {
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Write("Body text.");
        builder.MoveToHeaderFooter(type);
        builder.Write("Page ");
        builder.InsertField("PAGE");
        builder.InsertImage(Pixel);
        source.Save(_workspace.File("input.docx"), SaveFormat.Docx);
    }

    /// <summary>An input with body text whose primary footer <paramref name="write"/> writes, from its start.</summary>
    private void CreateFooter(Action<DocumentBuilder> write)
    {
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Write("Body text.");
        builder.MoveToHeaderFooter(HeaderFooterType.FooterPrimary);
        write(builder);
        source.Save(_workspace.File("input.docx"), SaveFormat.Docx);
    }

    // A one-pixel PNG.
    private static byte[] Pixel { get; } = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4z8DwHwAFAAH/iZk9HQAAAABJRU5ErkJggg==");

    /// <summary>The edit failed with OPS_INVALID, its message names the reason and its hint each way out.</summary>
    private static void AssertRefused(CliResult result, string reason, params string[] hints)
    {
        Assert.NotEqual(0, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdOut.Length > 0 ? result.StdOut : result.StdErr)!["error"]!;
        Assert.Equal("OPS_INVALID", error["code"]!.GetValue<string>());
        Assert.Contains(reason, error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        foreach (string hint in hints)
        {
            Assert.Contains(hint, error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        }
    }

    private CliResult Edit(string ops, params string[] options) =>
        _workspace.RunWithInput(ops, ["words", "edit", "input.docx", "--ops", "-", "--output-format", "json", .. options]);

    private JsonNode Inspected(string path)
    {
        CliResult inspected = _workspace.Run("words", "inspect", path, "--detail", "sections", "--output-format", "json");
        Assert.True(inspected.ExitCode == 0, inspected.StdErr + inspected.StdOut);
        return JsonNode.Parse(inspected.StdOut)!;
    }

    /// <summary>The paragraphs of the primary footer as a read reports them; a read leaves out evaluation mode's sentence.</summary>
    private string[] FooterLines(string path) => Lines(path, "footer");

    /// <summary>The paragraphs of the primary header or footer as a read reports them.</summary>
    private string[] Lines(string path, string location) =>
        [.. Inspected(path)["sections"]![0]!["headersFooters"]!.AsArray()
            .Single(item => item!["location"]!.GetValue<string>() == location && item["kind"]!.GetValue<string>() == "primary")!
            ["paragraphs"]!.AsArray().Select(static paragraph => paragraph!["text"]!.GetValue<string>())];

    private static Paragraph PageParagraph(Document document) =>
        (Paragraph)document.FirstSection.HeadersFooters[HeaderFooterType.FooterPrimary].Range.Fields.Cast<Field>()
            .Single(static field => field.Type == FieldType.FieldPage).Start.GetAncestor(NodeType.Paragraph);
}
