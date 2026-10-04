using Aspose.Words;
using Aspose.Words.Fields;
using Aspose.Words.Saving;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Views;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

public sealed class WordsFieldAndReviewTests
{
    [Fact]
    public void Review_ReportsEvaluationMarksSavedIntoTheFileOnlyWithALicense()
    {
        using var fixture = new WordsFixture();
        // The banner and footer sentence an unlicensed save writes, as a later licensed run reads them.
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Writeln("Created with an evaluation copy of Aspose.Words. To remove all limitations, you can use Free Temporary License https://products.aspose.com/words/temporary-license/");
        builder.Write("Clause one.");
        builder.MoveToHeaderFooter(HeaderFooterType.FooterPrimary);
        builder.Write("Evaluation Only. Created with Aspose.Words. Copyright 2003-2026 Aspose Pty Ltd.");
        string input = fixture.Temp.File("marked.docx");
        document.Save(input);
        var adapter = new WordsViewAdapter();
        var request = new ViewRenderRequest { View = WordsViews.Pages, MaxPartCount = 4, Purpose = ViewPurpose.Evidence };

        ViewManifest rendered = adapter.Render(fixture.Engine, input, request, new MemoryArtifactSink());
        ReviewFinding[] findings = [.. adapter.Assess(fixture.Engine, input, request, rendered).Findings!
            .Where(static finding => finding.Code == "WORDS_EVALUATION_MARKS")];

        // Without a license, opening the document adds the marks itself, so it is not checked.
        if (fixture.LicenseState == Aspose.Cli.Sdk.Licensing.LicenseState.Licensed)
        {
            ReviewFinding finding = Assert.Single(findings);
            Assert.StartsWith("2 paragraph(s)", finding.Message, StringComparison.Ordinal);
            Assert.Contains("license", finding.Hint, StringComparison.Ordinal);
        }
        else
        {
            Assert.Empty(findings);
        }
    }

    [Theory]
    [InlineData(true, "1 paragraph(s) hold the evaluation text a run without a license saved into the file, with its watermark. The document also ends with the notice that evaluation mode cut the document short there")]
    [InlineData(false, "The document ends with the notice that evaluation mode cut the document short there")]
    public void Review_SaysWhenEvaluationModeCutTheSavedDocumentShort(bool banner, string message)
    {
        using var fixture = new WordsFixture();
        var document = new Document();
        var builder = new DocumentBuilder(document);
        if (banner)
        {
            builder.Writeln("Created with an evaluation copy of Aspose.Words. To remove all limitations, you can use Free Temporary License https://products.aspose.com/words/temporary-license/");
        }

        builder.Writeln("Clause one.");
        builder.Write("This document was truncated here because it was created in the Evaluation Mode.");
        string input = fixture.Temp.File("truncated.docx");
        document.Save(input);
        var adapter = new WordsViewAdapter();
        var request = new ViewRenderRequest { View = WordsViews.Pages, MaxPartCount = 4, Purpose = ViewPurpose.Evidence };

        ViewManifest rendered = adapter.Render(fixture.Engine, input, request, new MemoryArtifactSink());
        ReviewFinding[] findings = [.. adapter.Assess(fixture.Engine, input, request, rendered).Findings!
            .Where(static finding => finding.Code == "WORDS_EVALUATION_MARKS")];

        // Without a license, opening the document adds the marks itself, so it is not checked.
        if (fixture.LicenseState == Aspose.Cli.Sdk.Licensing.LicenseState.Licensed)
        {
            Assert.StartsWith(message, Assert.Single(findings).Message, StringComparison.Ordinal);
        }
        else
        {
            Assert.Empty(findings);
        }
    }

    [Fact]
    public void InsertToc_UpdatesOnlyTheInsertedTableOfContents()
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Heading1;
        builder.Writeln("Scope");
        builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Normal;
        Field date = builder.InsertField("DATE", "Stored");
        builder.Writeln();
        string input = fixture.Temp.File("fields.docx");
        source.Save(input, SaveFormat.Docx);
        string output = fixture.Temp.File("toc.docx");

        fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops = [new InsertTocOp { At = new WordsTarget { Heading = "Scope" }, Position = "before" }],
        }, new WordsEditRequest { OutputPath = output });

        var document = new Document(output);
        Field[] fields = document.Range.Fields.Cast<Field>().ToArray();
        Assert.Equal("Stored", Assert.Single(fields, static field => field.Type == FieldType.FieldDate).Result);
        FieldToc toc = Assert.Single(fields.OfType<FieldToc>());
        Assert.Contains("Scope", toc.Result, StringComparison.Ordinal);
    }

    [Fact]
    public void InspectFields_ReadsResultsAsTheTextAReaderSees()
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.InsertTableOfContents("\\o \"1-3\" \\h \\z \\u");
        builder.InsertBreak(BreakType.PageBreak);
        builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Heading1;
        builder.Writeln("Scope");
        builder.Writeln("Terms");
        source.UpdateFields();
        string input = fixture.Temp.File("toc-fields.docx");
        source.Save(input, SaveFormat.Docx);

        DocumentInfoResult info = fixture.Engine.GetInfo(input, new DocumentInfoRequest { Details = ["fields"] });

        Assert.All(info.Fields!, static field => Assert.DoesNotContain(field.Result!, static c => c is '\u0013' or '\u0014' or '\u0015'));
        Assert.Equal("Scope\t2\rTerms\t2", Assert.Single(info.Fields!, static field => field.Type == "FieldTOC").Result);
        Assert.Equal("Scope\t2", info.Fields!.First(static field => field.Code!.Contains("_Toc", StringComparison.Ordinal)).Result);
    }

    [Fact]
    public void InsertField_FillsEveryPageFieldOfTheBatch()
    {
        using var fixture = new WordsFixture();
        string output = fixture.Temp.File("numpages.docx");

        fixture.Engine.ApplyOps(fixture.CreateReport(), new WordsOpsBatch
        {
            Ops =
            [
                new InsertFieldOp { At = new WordsTarget { Block = 1 }, Position = "after", Code = "PAGE" },
                new InsertFieldOp { At = new WordsTarget { Block = 1 }, Position = "after", Code = "NUMPAGES" },
            ],
        }, new WordsEditRequest { OutputPath = output });

        var document = new Document(output);
        Field[] fields = document.Range.Fields.Cast<Field>().ToArray();
        Assert.Equal("1", Assert.Single(fields, static field => field.Type == FieldType.FieldPage).Result);
        Assert.Equal("1", Assert.Single(fields, static field => field.Type == FieldType.FieldNumPages).Result);
    }

    [Fact]
    public void Inspect_NamesTheStoryOfFieldsAndImagesAndTheBlockOfThoseInTheBody()
    {
        using var fixture = new WordsFixture();
        byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.InsertField("MERGEFIELD Name");
        builder.InsertImage(png);
        builder.MoveToHeaderFooter(HeaderFooterType.FooterPrimary);
        builder.InsertField("PAGE");
        builder.InsertImage(png);
        string input = fixture.Temp.File("stories.docx");
        source.Save(input, SaveFormat.Docx);

        DocumentInfoResult info = fixture.Engine.GetInfo(input, new DocumentInfoRequest { Details = ["fields", "images"] });

        Assert.Equal(
            ["FieldMergeField:body:1", "FieldPage:headersFooters:"],
            info.Fields!.Where(static f => f.Type is "FieldMergeField" or "FieldPage").Select(static f => $"{f.Type}:{f.Scope}:{f.Block}").Order(StringComparer.Ordinal));
        Assert.Equal(
            ["body:1", "headersFooters:"],
            info.Images!.Where(static i => i.Width < 2).Select(static i => $"{i.Scope}:{i.Block}").Order(StringComparer.Ordinal));
    }

    [Fact]
    public void RemoveComments_RemovesEveryCommentAndItsRange()
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateReport();
        string commented = fixture.Temp.File("commented.docx");
        fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops = Enumerable.Range(1, 3)
                .Select(static block => (WordsOp)new AddCommentOp { At = new WordsTarget { Block = block }, Author = "A", Text = "Note" })
                .ToArray(),
        }, new WordsEditRequest { OutputPath = commented });
        string output = fixture.Temp.File("clean.docx");

        WordsEditResult result = fixture.Engine.ApplyOps(
            commented,
            new WordsOpsBatch { Ops = [new RemoveCommentsOp()] },
            new WordsEditRequest { OutputPath = output });

        Assert.Equal(3, Assert.Single(result.Applied).ItemsAffected);
        var document = new Document(output);
        Assert.Equal(0, document.GetChildNodes(NodeType.Comment, true).Count);
        Assert.Equal(0, document.GetChildNodes(NodeType.CommentRangeStart, true).Count);
        Assert.Equal(0, document.GetChildNodes(NodeType.CommentRangeEnd, true).Count);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData("Ann", 1)]
    public void AcceptRevisions_AcceptsAllOrOneAuthorsRevisions(string? author, int remaining)
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Writeln("Base");
        foreach (string name in new[] { "Ann", "Bob" })
        {
            source.StartTrackRevisions(name);
            builder.Writeln($"By {name}");
            source.StopTrackRevisions();
        }

        string input = fixture.Temp.File("revisions.docx");
        source.Save(input, SaveFormat.Docx);
        string output = fixture.Temp.File("accepted.docx");

        fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops = [new AcceptRevisionsOp { Author = author }],
        }, new WordsEditRequest { OutputPath = output });

        var document = new Document(output);
        Assert.Equal(remaining, document.Revisions.Select(static revision => revision.Author).Distinct().Count());
        Assert.All(document.Revisions, static revision => Assert.Equal("Bob", revision.Author));
    }
}
