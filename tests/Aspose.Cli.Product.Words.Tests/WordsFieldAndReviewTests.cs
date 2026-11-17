using Aspose.Words;
using Aspose.Words.Fields;
using Aspose.Words.Saving;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

public sealed class WordsFieldAndReviewTests
{
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

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops = [new InsertTocOp { At = new WordsTarget { Heading = "Scope" }, Position = "before" }],
            },
            Output = TestOutput.At(output),
        });

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

        WordsInspectResult info = WordsInspect.Run(fixture.Session, new WordsInspectRequest { Input = input, Details = ["fields"] });

        Assert.All(info.Fields!, static field => Assert.DoesNotContain(field.Result!, static c => c is '\u0013' or '\u0014' or '\u0015'));
        Assert.Equal("Scope\t2\rTerms\t2", Assert.Single(info.Fields!, static field => field.Kind == "FieldTOC").Result);
        Assert.Equal("Scope\t2", info.Fields!.First(static field => field.Code!.Contains("_Toc", StringComparison.Ordinal)).Result);
    }

    [Fact]
    public void InsertField_FillsEveryPageFieldOfTheBatch()
    {
        using var fixture = new WordsFixture();
        string output = fixture.Temp.File("numpages.docx");

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = fixture.CreateReport(),
            Batch = new WordsOpsBatch
            {
                Ops =
                [
                    new InsertFieldOp { At = new WordsTarget { Block = 1 }, Position = "after", Code = "PAGE" },
                    new InsertFieldOp { At = new WordsTarget { Block = 1 }, Position = "after", Code = "NUMPAGES" },
                ],
            },
            Output = TestOutput.At(output),
        });

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

        WordsInspectResult info = WordsInspect.Run(fixture.Session, new WordsInspectRequest { Input = input, Details = ["fields", "images"] });

        Assert.Equal(
            ["FieldMergeField:body:1", "FieldPage:headers-footers:"],
            info.Fields!.Where(static f => f.Kind is "FieldMergeField" or "FieldPage").Select(static f => $"{f.Kind}:{f.Scope}:{f.Block}").Order(StringComparer.Ordinal));
        Assert.Equal(
            ["body:1", "headers-footers:"],
            info.Images!.Where(static i => i.Width < 2).Select(static i => $"{i.Scope}:{i.Block}").Order(StringComparer.Ordinal));
    }

    [Fact]
    public void RemoveComments_RemovesEveryCommentAndItsRange()
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateReport();
        string commented = fixture.Temp.File("commented.docx");
        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops = Enumerable.Range(1, 3)
                    .Select(static block => (WordsOp)new AddCommentOp { Target = new WordsTarget { Block = block }, Author = "A", Text = "Note" })
                    .ToArray(),
            },
            Output = TestOutput.At(commented),
        });
        string output = fixture.Temp.File("clean.docx");

        WordsEditResult result = WordsEdit.Run(
            fixture.Session,
            new WordsEditRequest
            {
                Input = commented,
                Batch = new WordsOpsBatch { Ops = [new DeleteCommentsOp()] },
                Output = TestOutput.At(output),
            });

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

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops = [new AcceptRevisionsOp { Author = author }],
            },
            Output = TestOutput.At(output),
        });

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
        IReadOnlyList<WordsRevisionData> listed = WordsInspect.Run(fixture.Session, new WordsInspectRequest { Input = input, Details = ["revisions"] }).Revisions!;
        Assert.Equal(Enumerable.Range(1, listed.Count), listed.Select(static revision => revision.Revision));
        int[] Numbers(params string[] texts) => [.. listed.Where(revision => texts.Contains(revision.Text)).Select(static revision => revision.Revision)];
        string output = fixture.Temp.File("decided.docx");

        // Accepting the first change removes its deletion; later numbers still name the changes
        // inspect listed.
        WordsEditResult result = WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops =
                [
                    new AcceptRevisionsOp { Revisions = Numbers("30 days", "15 days") },
                    new AcceptRevisionsOp { Revisions = Numbers("10%", "20%") },
                    new RejectRevisionsOp { Revisions = Numbers("with us", "with them") },
                ],
            },
            Output = TestOutput.At(output),
        });

        // Each decision changed the one paragraph that held its revisions.
        Assert.All(result.Applied, static applied => Assert.Equal(1L, applied.ItemsAffected));
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

        CliException error = Assert.Throws<CliException>(() => WordsEdit.Run(
            fixture.Session,
            new WordsEditRequest
            {
                Input = input,
                Batch = new WordsOpsBatch
                {
                    Ops =
                    [
                        new ReplaceTextOp { Find = "15", Replace = "14" },
                        new AcceptRevisionsOp { Revisions = [1, 2] },
                    ],
                },
                Output = TestOutput.At(fixture.Temp.File("late.docx")),
            }));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains("before", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RevisionsByNumber_AreRefusedWhenAnEarlierDecisionTookThem()
    {
        using var fixture = new WordsFixture();
        string input = CreateReviewedContract(fixture);
        string output = fixture.Temp.File("twice.docx");

        CliException error = Assert.Throws<CliException>(() => WordsEdit.Run(
            fixture.Session,
            new WordsEditRequest
            {
                Input = input,
                Batch = new WordsOpsBatch { Ops = [new AcceptRevisionsOp(), new RejectRevisionsOp { Revisions = [1, 2] }] },
                Output = TestOutput.At(output),
            }));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains("earlier operation", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void AcceptRevisions_RefusesANumberInspectDoesNotList()
    {
        using var fixture = new WordsFixture();
        string input = CreateReviewedContract(fixture);

        CliException error = Assert.Throws<CliException>(() => WordsEdit.Run(
            fixture.Session,
            new WordsEditRequest
            {
                Input = input,
                Batch = new WordsOpsBatch { Ops = [new AcceptRevisionsOp { Revisions = [1, 7] }] },
                Output = TestOutput.At(fixture.Temp.File("none.docx")),
            }));

        Assert.Equal("REVISION_NOT_FOUND", error.Code.Name);
        Assert.Contains("6 exist", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReviewOperations_WarnWhenTheirAuthorMatchesNothing()
    {
        using var fixture = new WordsFixture();
        string input = CreateReviewedContract(fixture);
        string output = fixture.Temp.File("unchanged.docx");

        WordsEditResult result = WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops = [new AcceptRevisionsOp { Author = "Li" }, new DeleteCommentsOp { Author = "Li" }],
            },
            Output = TestOutput.At(output),
        });

        Assert.All(result.Applied, static applied => Assert.Equal(0L, applied.ItemsAffected));
        Warning[] warnings = [.. result.Warnings!.Where(static warning => warning.Code.Name == "OP_NO_EFFECT")];
        Assert.Equal(2, warnings.Length);
        Assert.Equal(["op-0001", "op-0002"], warnings.Select(static warning => warning.Details!["id"]!.GetValue<string>()));
        Assert.All(warnings, static warning => Assert.Equal("no-match", warning.Details!["reason"]!.GetValue<string>()));
        Assert.Contains("accept_revisions", warnings[0].Message, StringComparison.Ordinal);
        Assert.Contains("delete_comments", warnings[1].Message, StringComparison.Ordinal);
        Assert.Equal(6, new Document(output).Revisions.Count);
    }
}
