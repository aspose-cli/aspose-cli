using System.Globalization;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Words;
using Aspose.Words.Drawing;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>Messages and hints that must point the caller the right way.</summary>
public sealed class WordsDefectHintTests
{
    /// <summary>
    /// RTF stores a linked picture as an INCLUDEPICTURE field, so the reopened output has a field
    /// the edited document did not: the hint must not say the format loses fields.
    /// </summary>
    [Fact]
    public void Verify_OfAnRtfOutputThatGainedAField_DoesNotSayTheFormatLosesFields()
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Writeln("Clause one.");
        var linked = new Shape(source, ShapeType.Image) { Width = 40, Height = 40, WrapType = WrapType.Inline };
        linked.ImageData.SourceFullName = fixture.Temp.File("missing-logo.png");
        builder.InsertNode(linked);
        string input = fixture.Temp.File("linked.docx");
        source.Save(input, SaveFormat.Docx);

        WordsEditResult result = WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch { Ops = [new ReplaceTextOp { Find = "one", Replace = "two" }] },
            Output = TestOutput.At(fixture.Temp.File("linked.rtf")),
            Verify = true,
        });

        VerificationIssue issue = Assert.Single(result.Verification!.Issues, static issue => issue.Code == "FIELD_COUNT_MISMATCH");
        System.Text.RegularExpressions.Match counts = System.Text.RegularExpressions.Regex.Match(issue.Message, @"expected (\d+), found (\d+)");
        Assert.True(int.Parse(counts.Groups[2].Value, CultureInfo.InvariantCulture) > int.Parse(counts.Groups[1].Value, CultureInfo.InvariantCulture), issue.Message);
        Assert.DoesNotContain("may not keep", issue.Hint, StringComparison.Ordinal);
        Assert.DoesNotContain("did not survive", issue.Hint, StringComparison.Ordinal);
        Assert.Contains("INCLUDEPICTURE", issue.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportSections_OfAnEncryptedSourceWithoutItsPassword_NamesTheOperationField()
    {
        using var fixture = new WordsFixture();
        string source = fixture.CreateEncryptedDocument("secret", "source.docx");

        CliException error = Assert.Throws<CliException>(() => WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = fixture.CreateReport(),
            Batch = new WordsOpsBatch { Ops = [new ImportSectionsOp { Path = source }] },
            Output = TestOutput.At(fixture.Temp.File("imported.docx")),
        }));

        Assert.Equal(ErrorCodes.PasswordRequired, error.Code);
        Assert.Contains("\"sourcePasswordEnv\"", error.Hint, StringComparison.Ordinal);
        Assert.DoesNotContain("--password-env", error.Hint, StringComparison.Ordinal);
    }

    /// <summary>A truncation notice may come from an earlier save, so the message does not say this open cut the document.</summary>
    [Fact]
    public void TruncationDisclosure_DoesNotClaimTheDocumentWasCutWhenOpened()
    {
        string truncation = new WordsEvaluationProfile().Truncation;

        Assert.DoesNotContain("opens", truncation, StringComparison.Ordinal);
        Assert.Contains("truncation notice", truncation, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void SetProperties_WithAnEmptyCustomName_IsAnInvalidOperation(string name)
    {
        CliException error = Assert.Throws<CliException>(() => WordsOp.Catalog.Parse<WordsOpsBatch>(
            "{\"ops\":[{\"op\":\"set_properties\",\"custom\":{\"" + name + "\":\"x\"}}]}", Aspose.Cli.Generated.ProductJsonContext.Definition));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
    }

    private static WordsEditResult InsertTableWithStyle(WordsFixture fixture, string style) => WordsEdit.Run(fixture.Session, new WordsEditRequest
    {
        Input = fixture.CreateReport(),
        Batch = new WordsOpsBatch
        {
            Ops = [new InsertTableOp { At = new WordsTarget { Block = 2 }, Position = "after", RowCount = 1, ColumnCount = 1, TableStyle = style }],
        },
        Output = TestOutput.At(fixture.Temp.File("styled.docx")),
    });

    /// <summary>
    /// A name that is no table style of the document or of Word's, a paragraph style or a
    /// character style among them, is a name to correct rather than an engine failure.
    /// </summary>
    [Theory]
    [InlineData("Heading 1")]
    [InlineData("Default Paragraph Font")]
    [InlineData("Fancy Bogus")]
    public void InsertTable_WithoutATableStyleOfThatName_ListsOnlyTableStyles(string style)
    {
        using var fixture = new WordsFixture();

        CliException error = Assert.Throws<CliException>(() => InsertTableWithStyle(fixture, style));

        Assert.Equal(ErrorCodes.StyleNotFound, error.Code);
        Assert.Equal(style, error.Details!["requested"]!.GetValue<string>());
        string[] available = [.. error.Details!["available"]!.AsArray().Select(static node => node!.GetValue<string>())];
        Assert.Contains("Table Grid", available);
        Assert.DoesNotContain(available, static name => name is "Heading 1" or "Normal" or "Default Paragraph Font");
        Assert.Contains("'Table Grid'", error.Hint, StringComparison.Ordinal);
        Assert.Contains("'Grid Table 4'", error.Hint, StringComparison.Ordinal);
    }

    /// <summary>The built-in table styles need not be in the document; the guard for the list that leaves them out.</summary>
    [Theory]
    [InlineData("Table Normal")]
    [InlineData("Table Grid")]
    [InlineData("Grid Table 4")]
    public void InsertTable_TakesTheDocumentsAndWordsBuiltInTableStyles(string style)
    {
        using var fixture = new WordsFixture();

        InsertTableWithStyle(fixture, style);

        WordsInspectResult info = WordsInspect.Run(fixture.Session, new WordsInspectRequest { Input = fixture.Temp.File("styled.docx"), Details = [WordsInspectDetails.Tables] });
        Assert.Contains(info.Tables!, table => table.TableStyle == style);
    }

    private static WordsEditResult Annotate(WordsFixture fixture, string op, WordsTarget target) => WordsEdit.Run(fixture.Session, new WordsEditRequest
    {
        Input = fixture.CreateReport(),
        Batch = new WordsOpsBatch
        {
            Ops = [op == "add_comment"
                ? new AddCommentOp { Target = target, Author = "Reviewer", Text = "Check" }
                : new InsertBookmarkOp { Target = target, Name = "Anchor" }],
        },
        Output = TestOutput.At(fixture.Temp.File("annotated.docx")),
    });

    /// <summary>
    /// The report's table is block 5. Whichever way an address reaches it, the refusal says which
    /// block it reached and what that block is, and the hint gives a next step without an address.
    /// </summary>
    [Theory]
    [InlineData("add_comment", "comment", "Metric", null, null)]
    [InlineData("insert_bookmark", "bookmark", "Metric", null, null)]
    [InlineData("add_comment", "comment", null, 5, null)]
    [InlineData("insert_bookmark", "bookmark", "Revenue", null, 2)]
    public void Annotation_OnATable_NamesTheBlockAndPointsToAParagraph(string op, string annotation, string? find, int? block, int? nth)
    {
        using var fixture = new WordsFixture();

        CliException error = Assert.Throws<CliException>(() => Annotate(fixture, op, new WordsTarget { Find = find, Block = block, Nth = nth }));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains($"{op} must target a paragraph block, but block/5 is a table", error.Message, StringComparison.Ordinal);
        Assert.Contains($"A {annotation} anchors only to a body paragraph", error.Hint, StringComparison.Ordinal);
        Assert.Contains("the paragraph before or after the table", error.Hint, StringComparison.Ordinal);
        Assert.DoesNotContain("{", error.Hint, StringComparison.Ordinal);
    }

    /// <summary>The guard for the refusal above: a find text in a body paragraph, also when the same text is in a table later on.</summary>
    [Theory]
    [InlineData("add_comment", "Operations")]
    [InlineData("add_comment", "Revenue")]
    [InlineData("insert_bookmark", "Operations")]
    [InlineData("insert_bookmark", "Revenue")]
    public void Annotation_OnAParagraphFoundByText_IsApplied(string op, string find)
    {
        using var fixture = new WordsFixture();

        Assert.Equal(1L, Assert.Single(Annotate(fixture, op, new WordsTarget { Find = find }).Applied).ItemsAffected);
    }

    /// <summary>A revision across a paragraph boundary shows the boundary as a line feed, not as Word's raw paragraph mark.</summary>
    [Fact]
    public void InspectRevisions_ShowsAParagraphBreakAsALineFeed()
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Writeln("First.");
        builder.Write("Last.");
        source.StartTrackRevisions("Reviewer", new DateTime(2026, 1, 1));
        builder.MoveToDocumentStart();
        builder.Writeln("Added line.");
        builder.Write("Added start ");
        source.StopTrackRevisions();
        string input = fixture.Temp.File("tracked.docx");
        source.Save(input, SaveFormat.Docx);

        WordsInspectResult result = WordsInspect.Run(fixture.Session, new WordsInspectRequest { Input = input, Details = [WordsInspectDetails.Revisions] });

        WordsRevisionData inserted = Assert.Single(result.Revisions!, static revision => revision.Kind == WordsRevisionKinds.Insertion);
        Assert.DoesNotContain('\r', inserted.Text!);
        Assert.Contains("Added line.\nAdded start", inserted.Text!, StringComparison.Ordinal);
    }
}
