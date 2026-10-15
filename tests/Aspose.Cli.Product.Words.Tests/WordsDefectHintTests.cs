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

        VerificationIssue issue = Assert.Single(result.Verification!.Issues, static issue => issue.Code == "FIELD_COUNT_CHANGED");
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
