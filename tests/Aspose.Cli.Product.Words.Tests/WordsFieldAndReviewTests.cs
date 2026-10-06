using Aspose.Words;
using Aspose.Words.Fields;
using Aspose.Words.Saving;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
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
        builder.Writeln(WordsFixture.BannerText);
        builder.Write("Clause one.");
        builder.MoveToHeaderFooter(HeaderFooterType.FooterPrimary);
        builder.Write(WordsFixture.FooterMarkText);
        string input = fixture.Temp.File("marked.docx");
        document.Save(input);

        ReviewFinding[] findings = EvaluationFindings(fixture, input);

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
            builder.Writeln(WordsFixture.BannerText);
        }

        builder.Writeln("Clause one.");
        builder.Write("This document was truncated here because it was created in the Evaluation Mode.");
        string input = fixture.Temp.File("truncated.docx");
        document.Save(input);

        ReviewFinding[] findings = EvaluationFindings(fixture, input);

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

    /// <summary>The WORDS_EVALUATION_MARKS findings of a page review of an unencrypted document.</summary>
    private static ReviewFinding[] EvaluationFindings(WordsFixture fixture, string input)
    {
        var adapter = new WordsViewAdapter();
        var request = new ViewRenderRequest { View = WordsViews.Pages, MaxPartCount = 4, Purpose = ViewPurpose.Evidence };
        ViewManifest rendered = adapter.Render(fixture.Engine, input, request, new MemoryArtifactSink());
        Assert.False(rendered.SourceEncrypted);
        return [.. adapter.Assess(fixture.Engine, input, request, rendered).Findings!
            .Where(static finding => finding.Code == "WORDS_EVALUATION_MARKS")];
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

    /// <summary>Three replacements one reviewer tracked: each a deletion and an insertion.</summary>
    private static string CreateReviewedContract(WordsFixture fixture)
    {
        var source = new Document();
        new DocumentBuilder(source).Write("Payment in 30 days. Cap is 10%. IP stays with us.");
        source.StartTrackRevisions("Li (B)");
        source.Range.Replace("30 days", "15 days");
        source.Range.Replace("10%", "20%");
        source.Range.Replace("with us", "with them");
        source.StopTrackRevisions();
        string input = fixture.Temp.File("reviewed.docx");
        source.Save(input, SaveFormat.Docx);
        return input;
    }

    [Fact]
    public void AcceptAndRejectRevisions_TakeTheNumbersInspectListsBeforeTheBatch()
    {
        using var fixture = new WordsFixture();
        string input = CreateReviewedContract(fixture);
        IReadOnlyList<RevisionData> listed = fixture.Engine.GetInfo(input, new DocumentInfoRequest { Details = ["revisions"] }).Revisions!;
        Assert.Equal(Enumerable.Range(1, listed.Count), listed.Select(static revision => revision.Revision));
        int[] Numbers(params string[] texts) => [.. listed.Where(revision => texts.Contains(revision.Text)).Select(static revision => revision.Revision)];
        string output = fixture.Temp.File("decided.docx");

        // Accepting the first change removes its deletion; later numbers still name the changes
        // inspect listed.
        WordsEditResult result = fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops =
            [
                new AcceptRevisionsOp { Revisions = Numbers("30 days", "15 days") },
                new AcceptRevisionsOp { Revisions = Numbers("10%", "20%") },
                new RejectRevisionsOp { Revisions = Numbers("with us", "with them") },
            ],
        }, new WordsEditRequest { OutputPath = output });

        Assert.All(result.Applied, static applied => Assert.Equal(2L, applied.ItemsAffected));
        var document = new Document(output);
        // Under evaluation the body starts with the engine's banner.
        Assert.Contains("Payment in 15 days. Cap is 20%. IP stays with us.", document.FirstSection.Body.Paragraphs.Cast<Paragraph>().Select(static paragraph => paragraph.GetText().Trim()));
        Assert.Equal(0, document.Revisions.Count);
    }

    [Fact]
    public void RevisionsByNumber_AreRefusedAfterAnOperationThatEditsTheDocument()
    {
        // replace_text inside an inserted run splits it, and the split-off part would stay
        // undecided.
        using var fixture = new WordsFixture();
        string input = CreateReviewedContract(fixture);

        CliException error = Assert.Throws<CliException>(() => fixture.Engine.ApplyOps(
            input,
            new WordsOpsBatch
            {
                Ops =
                [
                    new ReplaceTextOp { Find = "15", Replace = "14" },
                    new AcceptRevisionsOp { Revisions = [1, 2] },
                ],
            },
            new WordsEditRequest { OutputPath = fixture.Temp.File("late.docx") }));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains("before", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RevisionsByNumber_AreRefusedWhenAnEarlierDecisionTookThem()
    {
        using var fixture = new WordsFixture();
        string input = CreateReviewedContract(fixture);
        string output = fixture.Temp.File("twice.docx");

        CliException error = Assert.Throws<CliException>(() => fixture.Engine.ApplyOps(
            input,
            new WordsOpsBatch { Ops = [new AcceptRevisionsOp(), new RejectRevisionsOp { Revisions = [1, 2] }] },
            new WordsEditRequest { OutputPath = output }));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains("earlier operation", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void AcceptRevisions_RefusesANumberInspectDoesNotList()
    {
        using var fixture = new WordsFixture();
        string input = CreateReviewedContract(fixture);

        CliException error = Assert.Throws<CliException>(() => fixture.Engine.ApplyOps(
            input,
            new WordsOpsBatch { Ops = [new AcceptRevisionsOp { Revisions = [1, 7] }] },
            new WordsEditRequest { OutputPath = fixture.Temp.File("none.docx") }));

        Assert.Equal("REVISION_NOT_FOUND", error.Code.Name);
        Assert.Contains("6 exist", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReviewOperations_WarnWhenTheirAuthorMatchesNothing()
    {
        using var fixture = new WordsFixture();
        string input = CreateReviewedContract(fixture);
        string output = fixture.Temp.File("unchanged.docx");

        WordsEditResult result = fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops = [new AcceptRevisionsOp { Author = "Li" }, new RemoveCommentsOp { Author = "Li" }],
        }, new WordsEditRequest { OutputPath = output });

        Assert.All(result.Applied, static applied => Assert.Equal(0L, applied.ItemsAffected));
        Warning[] warnings = [.. result.Warnings!.Where(static warning => warning.Code == "AUTHOR_NO_MATCH")];
        Assert.Equal(2, warnings.Length);
        Assert.Contains("accept_revisions", warnings[0].Message, StringComparison.Ordinal);
        Assert.Contains("'Li (B)'", warnings[0].Hint, StringComparison.Ordinal);
        Assert.Contains("remove_comments", warnings[1].Message, StringComparison.Ordinal);
        Assert.Equal(6, new Document(output).Revisions.Count);
    }
}
