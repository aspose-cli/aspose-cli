using System.Text.Json.Nodes;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Tables;
using Aspose.Cli.TestKit.Tables;
using Aspose.Words;
using Aspose.Words.Markup;
using Aspose.Words.Saving;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// What the Words table does around the rows: where objects sit and in which order read lists
/// them, what an add, a clone and an action leave, and that reading and counting change nothing
/// the engine saves. The engine runs in this process, so the cases run licensed only.
/// </summary>
public sealed class WordsTableBehaviorTests
{
    private const string Body = "section/0/body";

    [Fact]
    public void Edit_SavesThePackageTheEngineAloneSavesForTheSameChange()
    {
        using var fixture = new WordsFixture();
        WordsFixture.RequireLicense();
        string input = fixture.CreateProposal();
        string alone = fixture.File("alone.docx");
        var document = new Document(input);
        document.FirstSection.Body.Paragraphs[2].Runs[0].Text = "Changed.";
        document.FirstSection.Body.Paragraphs[0].ParagraphFormat.Alignment = ParagraphAlignment.Center;
        document.Save(alone, SaveOptions.CreateSaveOptions(SaveFormat.Docx));
        string ops = $$$"""{"ops":[{"set":"{{{Body}}}/paragraph/2/run/0","props":{"text":"Changed."}},{"set":"{{{Body}}}/paragraph/0","props":{"paragraphFormat.alignment":"Center"}}]}""";

        WordsEdit.Run(fixture.Session, WordsFixture.Edit(input, fixture.File("edited.docx"), ops));

        string[] differ = SavedPackage.Differences(alone, fixture.File("edited.docx"));
        Assert.True(differ.Length == 0, "Parts that differ from the engine's own save: " + string.Join(", ", differ));
    }

    /// <summary>
    /// An output password for an input whose format cannot carry one is refused by the password
    /// itself: a request that names no option for it is refused as <c>--encrypt</c>.
    /// </summary>
    [Fact]
    public void Edit_WithAnOutputPassword_RefusesAFormatThatCannotCarryOne()
    {
        using var fixture = new WordsFixture();
        string input = fixture.File("plain.rtf");
        string output = fixture.File("edited.rtf");
        TableEditRequest request = WordsFixture.Edit(input, output, $$$"""{"ops":[{"set":"{{{Body}}}/paragraph/0","props":{"paragraphFormat.alignment":"Center"}}]}""")
            with { EncryptPassword = new Secret("output-secret") };
        WordsSession session = fixture.Session;
        new Document().Save(input, SaveFormat.Rtf);

        CliException error = Assert.Throws<CliException>(() => WordsEdit.Run(session, request));

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.Equal("--encrypt", error.Details!["option"]!.GetValue<string>());
        Assert.Contains("'rtf'", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void Read_ListsParagraphsAndTablesInTheEnginesOrder()
    {
        using var fixture = new WordsFixture();
        WordsReadResult read = WordsRead.Run(fixture.Session, new TableReadRequest { Input = fixture.CreateProposal(), Kinds = ["paragraph", "table"] });

        Assert.Equal(
            [.. Enumerable.Range(0, 7).Select(static index => $"{Body}/paragraph/{index}"), $"{Body}/table/0",
                .. Enumerable.Range(0, 4).SelectMany(static row => Enumerable.Range(0, 3).Select(cell => $"{Body}/table/0/row/{row}/cell/{cell}/paragraph/0")),
                $"{Body}/paragraph/7", $"{Body}/paragraph/8"],
            read.Objects.Select(static found => found.At));
        Assert.False(read.Inventory.Read.ContainsKey("pages") || read.Inventory.NotRead.ContainsKey("pages"));
    }

    [Fact]
    public void Read_WithoutKind_ListsTheStructure_AndNamesWhatItLeftOut()
    {
        using var fixture = new WordsFixture();
        WordsReadResult read = WordsRead.Run(fixture.Session, new TableReadRequest { Input = fixture.CreateProposal() });

        Assert.Equal(["document", "section/0", $"{Body}", $"{Body}/table/0"], read.Objects.Select(static found => found.At));
        Assert.Equal(WordsKinds.ListedWhenNamed, read.Window.NotListedUnlessNamed);
        Assert.DoesNotContain("pageCount", read.Objects[0].Props.Select(static row => row.Key));
    }

    [Fact]
    public void AddBefore_PutsTheParagraphBeforeItsSibling_AndTheRunInIt()
    {
        using var fixture = new WordsFixture();
        string ops = $$$"""
            {"ops":[
              {"add":"paragraph","in":"{{{Body}}}","args":{"before":"{{{Body}}}/paragraph/7"},"props":{"paragraphFormat.styleName":"Heading 1"}},
              {"add":"run","in":"{{{Body}}}/paragraph/7","args":{"text":"风险 Risks"}},
              {"add":"run","in":"{{{Body}}}/table/0/row/1/cell/2/paragraph/0","args":{"text":"王芳 Wang Fang"}}
            ]}
            """;

        WordsEditResult result = WordsEdit.Run(fixture.Session, WordsFixture.Edit(fixture.CreateProposal(), fixture.File("out.docx"), ops));

        Assert.Equal("风险 Risks\r", result.Ops[0].Now!["text"]!.GetValue<string>());
        Assert.Equal("Heading 1", result.Ops[0].Now!["paragraphFormat.styleName"]!.GetValue<string>());
        Assert.Equal($"{Body}/table/0/row/1/cell/2/paragraph/0/run/0", result.Ops[2].At);
        var saved = new Document(fixture.File("out.docx"));
        Assert.Equal("预算 Budget\r", saved.FirstSection.Body.Paragraphs[8].GetText());
        Assert.Equal([21, 22], result.Counts["paragraphs"]);
    }

    [Fact]
    public void AddField_AppendsItToTheParagraph_AndReadsItBack()
    {
        using var fixture = new WordsFixture();
        string ops = """
            {"ops":[
              {"add":"headerFooter","in":"section/0","args":{"headerFooterType":"FooterPrimary"}},
              {"add":"paragraph","in":"section/0/headerFooter/FooterPrimary","props":{"paragraphFormat.alignment":"Center"}},
              {"add":"field","in":"section/0/headerFooter/FooterPrimary/paragraph/0","args":{"fieldCode":"PAGE"}}
            ]}
            """;

        WordsEditResult result = WordsEdit.Run(fixture.Session, WordsFixture.Edit(fixture.CreateProposal(), fixture.File("out.docx"), ops));

        JsonObject field = result.Ops[2].Now!;
        Assert.Equal("FieldPage", field["type"]!.GetValue<string>());
        Assert.Equal("PAGE", field["fieldCode"]!.GetValue<string>());
        Assert.Equal("1", field["result"]!.GetValue<string>());
    }

    [Fact]
    public void Replace_ReturnsTheEnginesCount()
    {
        using var fixture = new WordsFixture();

        WordsEditResult result = WordsEdit.Run(fixture.Session, WordsFixture.Edit(fixture.CreateProposal(), fixture.File("out.docx"),
            """{"ops":[{"do":"replace","args":{"pattern":"Contoso","replacement":"Fabrikam"}},{"do":"replace","args":{"pattern":"Fabrikam","replacement":"F","node":"section/0/body/paragraph/0"}}]}"""));

        Assert.Equal(4, result.Ops[0].Returned!.GetValue<int>());
        Assert.Equal(1, result.Ops[1].Returned!.GetValue<int>());
    }

    [Fact]
    public void Clone_InsertsTheCopyBeforeItsSibling_AndReturnsItsAddress()
    {
        using var fixture = new WordsFixture();
        string ops = $$$"""{"ops":[{"do":"clone","args":{"node":"{{{Body}}}/table/0/row/1","parent":"{{{Body}}}/table/0","before":"{{{Body}}}/table/0/row/2","isCloneChildren":true}}]}""";

        WordsEditResult result = WordsEdit.Run(fixture.Session, WordsFixture.Edit(fixture.CreateProposal(), fixture.File("out.docx"), ops));

        Assert.Equal($"{Body}/table/0/row/2", result.Ops[0].At);
        Assert.Equal([4, 5], result.Counts["rows"]);
        Assert.Equal("设计 Design\a2026-03-01\a\a\a", new Document(fixture.File("out.docx")).FirstSection.Body.Tables[0].Rows[2].GetText());
    }

    [Fact]
    public void ContentControl_IsASibling_WhoseContentIsListedOnceAfterIt()
    {
        using var fixture = new WordsFixture();
        WordsFixture.RequireLicense();
        string input = fixture.File("control.docx");
        var document = new Document();
        document.FirstSection.Body.AppendParagraph("Before");
        var control = new StructuredDocumentTag(document, SdtType.RichText, MarkupLevel.Block) { Tag = "block" };
        control.RemoveAllChildren();
        control.AppendChild(new Paragraph(document));
        ((Paragraph)control.FirstChild).AppendChild(new Run(document, "Inside"));
        document.FirstSection.Body.AppendChild(control);
        document.FirstSection.Body.AppendParagraph("After");
        document.Save(input);

        WordsReadResult read = WordsRead.Run(fixture.Session, new TableReadRequest { Input = input, Kinds = ["paragraph", "sdt"] });
        WordsEditResult added = WordsEdit.Run(fixture.Session, WordsFixture.Edit(input, fixture.File("out.docx"),
            $$$"""{"ops":[{"add":"paragraph","in":"{{{Body}}}/sdt/0"},{"add":"run","in":"{{{Body}}}/sdt/0/paragraph/1","args":{"text":"Second"}}]}"""));
        WordsReadResult range = WordsRead.Run(fixture.Session, new TableReadRequest { Input = fixture.File("out.docx"), At = $"{Body}/sdt/0/range" });
        // The paragraph added in the control has two addresses, one object: a change made through
        // its address in the control reads back through the address read lists.
        WordsEditResult changed = WordsEdit.Run(fixture.Session, WordsFixture.Edit(fixture.File("out.docx"), fixture.File("changed.docx"),
            $$$"""{"ops":[{"set":"{{{Body}}}/sdt/0/paragraph/1/run/0","props":{"text":"Changed"}}]}"""));
        WordsReadResult listed = WordsRead.Run(fixture.Session, new TableReadRequest { Input = fixture.File("changed.docx"), Kinds = ["paragraph"] });
        WordsReadResult inParent = WordsRead.Run(fixture.Session, new TableReadRequest { Input = fixture.File("changed.docx"), At = $"{Body}/paragraph/3" });
        // remove_self_only takes the control away and leaves its content.
        WordsEdit.Run(fixture.Session, WordsFixture.Edit(input, fixture.File("unwrapped.docx"), $$$"""{"ops":[{"do":"remove_self_only","args":{"sdt":"{{{Body}}}/sdt/0"}}]}"""));
        var unwrapped = new Document(fixture.File("unwrapped.docx"));

        Assert.Equal([$"{Body}/paragraph/0", $"{Body}/paragraph/1", $"{Body}/sdt/0", $"{Body}/paragraph/2", $"{Body}/paragraph/3"], read.Objects.Select(static found => found.At));
        Assert.Equal("Inside\r", read.Objects[3].Props["text"]!.GetValue<string>());
        Assert.Equal($"{Body}/sdt/0/paragraph/1/run/0", added.Ops[1].At);
        Assert.Equal("Inside\rSecond\r", Assert.Single(range.Objects).Props["text"]!.GetValue<string>());
        Assert.Equal($"{Body}/sdt/0/paragraph/1/run/0", Assert.Single(changed.Ops).At);
        Assert.Equal([$"{Body}/paragraph/0", $"{Body}/paragraph/1", $"{Body}/paragraph/2", $"{Body}/paragraph/3", $"{Body}/paragraph/4"], listed.Objects.Select(static found => found.At));
        Assert.Equal("Changed\r", inParent.Objects[0].Props["text"]!.GetValue<string>());
        Assert.Equal(0, unwrapped.GetChildNodes(NodeType.StructuredDocumentTag, true).Count);
        Assert.Contains("Inside", unwrapped.GetText(), StringComparison.Ordinal);
    }

    /// <summary>
    /// A content control in a control, block in block, inline in inline and block in block in a
    /// cell, is listed once below the control that holds it: the controls read lists are as many as
    /// the inventory counts, and each address reads back the control it names.
    /// </summary>
    [Fact]
    public void NestedContentControls_AreEachListedOnce_BelowTheControlThatHoldsThem()
    {
        WordsFixture.RequireLicense();
        using var fixture = new WordsFixture();
        string input = fixture.File("nested.docx");
        var document = new Document();
        Body body = document.FirstSection.Body;
        body.RemoveAllChildren();
        StructuredDocumentTag Control(MarkupLevel level, string tag, params Node[] content)
        {
            var control = new StructuredDocumentTag(document, SdtType.RichText, level) { Tag = tag };
            control.RemoveAllChildren();
            foreach (Node node in content)
            {
                control.AppendChild(node);
            }
            return control;
        }
        Paragraph Text(string text)
        {
            var paragraph = new Paragraph(document);
            paragraph.AppendChild(new Run(document, text));
            return paragraph;
        }
        body.AppendChild(Control(MarkupLevel.Block, "outer", Text("Outer"), Control(MarkupLevel.Block, "inner", Text("Inner"))));
        var line = new Paragraph(document);
        line.AppendChild(Control(MarkupLevel.Inline, "inline-outer", new Run(document, "Outer "), Control(MarkupLevel.Inline, "inline-inner", new Run(document, "inner"))));
        body.AppendChild(line);
        var builder = new DocumentBuilder(document);
        builder.MoveToDocumentEnd();
        Aspose.Words.Tables.Table table = builder.StartTable();
        builder.InsertCell();
        builder.EndRow();
        builder.EndTable();
        table.FirstRow.FirstCell.AppendChild(Control(MarkupLevel.Block, "cell-outer", Control(MarkupLevel.Block, "cell-inner", Text("In the cell"))));
        body.AppendChild(new Paragraph(document));
        document.Save(input);

        WordsReadResult read = WordsRead.Run(fixture.Session, new TableReadRequest { Input = input, Kinds = ["sdt"] });

        Assert.Equal(
            [$"{Body}/sdt/0", $"{Body}/sdt/0/sdt/0", $"{Body}/paragraph/2/sdt/0", $"{Body}/paragraph/2/sdt/0/sdt/0", $"{Body}/table/0/row/0/cell/0/sdt/0", $"{Body}/table/0/row/0/cell/0/sdt/0/sdt/0"],
            read.Objects.Select(static found => found.At));
        Assert.Equal(read.Inventory.Read["structuredDocumentTags"], read.Objects.Count);
        Assert.Equal(["outer", "inner", "inline-outer", "inline-inner", "cell-outer", "cell-inner"],
            read.Objects.Select(found => Assert.Single(WordsRead.Run(fixture.Session, new TableReadRequest { Input = input, At = found.At, Kinds = ["sdt"] }).Objects, item => item.At == found.At).Props["tag"]!.GetValue<string>()));
    }

    /// <summary>
    /// A field in a footnote, a comment or a text box anchored in a paragraph is listed once, below
    /// its own story's paragraph, and the anchor paragraph lists none of them: the fields read lists
    /// are as many as the inventory counts, and a set of the body's own field leaves the footnote's.
    /// </summary>
    [Fact]
    public void FieldsOfAnchoredStories_AreListedOnce_InTheirOwnParagraph()
    {
        WordsFixture.RequireLicense();
        using var fixture = new WordsFixture();
        string input = fixture.File("anchored.docx");
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Write("Anchor ");
        Paragraph anchor = builder.CurrentParagraph;
        Aspose.Words.Notes.Footnote footnote = builder.InsertFootnote(Aspose.Words.Notes.FootnoteType.Footnote, "Note ");
        builder.MoveTo(anchor);
        Aspose.Words.Drawing.Shape box = builder.InsertShape(Aspose.Words.Drawing.ShapeType.TextBox, 100, 50);
        box.AppendChild(new Paragraph(document));
        var comment = new Comment(document, "A", "A", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified));
        comment.Paragraphs.Add(new Paragraph(document));
        anchor.AppendChild(comment);
        var own = new Paragraph(document);
        anchor.ParentNode.InsertAfter(own, anchor);
        builder.MoveTo(own);
        builder.InsertField("AUTHOR", "Body author");
        builder.MoveTo(footnote.FirstParagraph);
        builder.InsertField("PAGE");
        builder.MoveTo(comment.FirstParagraph);
        builder.InsertField("DATE");
        builder.MoveTo(box.FirstParagraph);
        builder.InsertField("NUMPAGES");
        document.Save(input);

        WordsReadResult read = WordsRead.Run(fixture.Session, new TableReadRequest { Input = input, Kinds = ["field"] });
        string[] listed = [.. read.Objects.Select(static found => found.At)];
        string body = Assert.Single(listed, static at => at.StartsWith($"{Body}/", StringComparison.Ordinal) && !at.Contains("/shape/", StringComparison.Ordinal));
        WordsEdit.Run(fixture.Session, WordsFixture.Edit(input, fixture.File("out.docx"), $$$"""{"ops":[{"set":"{{{body}}}","props":{"result":"Changed"}}]}"""));
        var saved = new Document(fixture.File("out.docx"));

        Assert.Equal(read.Inventory.Read["fields"], read.Objects.Count);
        Assert.Equal($"{Body}/paragraph/1/field/0", body);
        Assert.DoesNotContain(listed, static at => at.StartsWith($"{Body}/paragraph/0/field/", StringComparison.Ordinal));
        Assert.Contains($"{Body}/paragraph/0/shape/0/paragraph/0/field/0", listed);
        Assert.Contains("comment/0/paragraph/0/field/0", listed);
        Assert.Contains("footnote/0/paragraph/0/field/0", listed);
        Assert.Equal("Changed", saved.FirstSection.Body.Paragraphs[1].Range.Fields[0].Result);
        Assert.Equal("1", ((Aspose.Words.Notes.Footnote)saved.GetChild(NodeType.Footnote, 0, true)).Range.Fields[0].Result);
    }

    [Fact]
    public void MergedCells_AreKeptAsAPair()
    {
        using var fixture = new WordsFixture();
        string ops = $$$"""{"ops":[{"set":"{{{Body}}}/table/0/row/2/cell/1","props":{"cellFormat.verticalMerge":"First"}},{"set":"{{{Body}}}/table/0/row/3/cell/1","props":{"cellFormat.verticalMerge":"Previous"}}]}""";

        WordsEditResult result = WordsEdit.Run(fixture.Session, WordsFixture.Edit(fixture.CreateProposal(), fixture.File("out.docx"), ops));

        Assert.Equal("First", result.Ops[0].Now!["cellFormat.verticalMerge"]!.GetValue<string>());
        Assert.Equal("Previous", result.Ops[1].Now!["cellFormat.verticalMerge"]!.GetValue<string>());
    }

    [Fact]
    public void Layout_IsReadOnlyByItsAddress()
    {
        using var fixture = new WordsFixture();

        WordsReadResult read = WordsRead.Run(fixture.Session, new TableReadRequest { Input = fixture.CreateProposal(), At = "layout" });

        Assert.Equal(1, Assert.Single(read.Objects).Props["pageCount"]!.GetValue<int>());
    }

    [Theory]
    [InlineData("""{"ops":[{"add":"run","in":"section/0/body/table/0/row/0/cell/0","args":{"text":"x"}}]}""", "OPS_INVALID")]
    [InlineData("""{"ops":[{"add":"paragraph","in":"section/0/body","args":{"before":"section/0/body/table/0/row/0"}}]}""", "OPS_INVALID")]
    [InlineData("""{"ops":[{"set":"section/0/body/paragraph/0/run/5","props":{"text":"x"}}]}""", "ADDRESS_NOT_FOUND")]
    [InlineData("""{"ops":[{"set":"section/0/body/paragraph/0","props":{"listFormat.list":42}}]}""", "ADDRESS_NOT_FOUND")]
    [InlineData("""{"ops":[{"add":"bookmark","args":{"paragraph":"section/0/body/paragraph/1","name":"B1"}},{"add":"bookmark","args":{"paragraph":"section/0/body/paragraph/3","name":"b1"}}]}""", "ADDRESS_AMBIGUOUS")]
    [InlineData("""{"ops":[{"add":"paragraph","in":"section/0/body","args":{"before":"section/0/body/table/0/row/0/cell/0/paragraph/0"}}]}""", "ENGINE_ERROR")]
    [InlineData("""{"ops":[{"set":"section/0/body/paragraph/0","props":{"paragraphFormat.styleName":"No Such Style"}}]}""", "ENGINE_ERROR")]
    [InlineData("""{"ops":[{"add":"field","in":"section/0/body/paragraph/2","args":{"fieldCode":"PAGE"}},{"do":"update_toc_page_numbers","args":{"field":"section/0/body/paragraph/2/field/0"}}]}""", "ADDRESS_NOT_FOUND")]
    public void Refused_Operation_WritesNothing(string ops, string code)
    {
        using var fixture = new WordsFixture();
        string output = fixture.File("out.docx");

        CliException refused = Assert.Throws<CliException>(() => WordsEdit.Run(fixture.Session, WordsFixture.Edit(fixture.CreateProposal(), output, ops)));

        Assert.Equal(code, refused.Code.Name);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void EachEditableFormat_IsWrittenBackInItsFormat()
    {
        using var fixture = new WordsFixture();
        WordsFixture.RequireLicense();
        var source = new Document(fixture.CreateProposal());
        foreach (FormatDescriptor format in WordsFormats.Editable)
        {
            string input = fixture.File($"in-{format.Id}{format.Extensions[0]}");
            source.Save(input, WordsEngineFormats.Options(format.Id));
            string output = fixture.File($"out-{format.Id}{format.Extensions[0]}");

            WordsEditResult result = WordsEdit.Run(fixture.Session, WordsFixture.Edit(input, output, """{"ops":[{"set":"document","props":{"builtInDocumentProperties.title":"T"}}]}"""));

            Assert.Equal(format.Id, result.Output.Format);
            Assert.Equal("T", result.Ops[0].Now!["builtInDocumentProperties.title"]!.GetValue<string>());
        }
    }

    [Fact]
    public void Schema_WritesTheFormsOfKindsThatHoldThemselvesOnce()
    {
        string directory = TableSchema.Of(WordsModule.Manifest.Id, WordsTable.Definition).Views[TableSchema.DirectoryView];
        JsonNode kinds = JsonNode.Parse(directory)!["kinds"]!;

        Assert.Equal(["{paragraph}/run/<index>", "{sdt}/run/<index>"], kinds["run"]!["addresses"]!.AsArray().Select(static form => form!.GetValue<string>()).Order(StringComparer.Ordinal));
        Assert.True(directory.Length < 30_000, $"The directory is {directory.Length} characters.");
    }
}
