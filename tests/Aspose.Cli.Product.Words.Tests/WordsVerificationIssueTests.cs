using Aspose.Cli.Sdk.Contracts;
using Aspose.Words;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// Plain text keeps no fields, revisions or protection, so a verified edit saved as
/// <c>.txt</c> loses exactly the state each check compares.
/// </summary>
public sealed class WordsVerificationIssueTests
{
    [Fact]
    public void Verify_ReportsFieldsLostOnReopen()
    {
        using var fixture = new WordsFixture();
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Writeln("Revenue increased by twelve percent.");
        builder.InsertField("DATE");
        string input = fixture.Temp.File("fields.docx");
        document.Save(input, SaveFormat.Docx);

        VerificationIssue issue = VerifyAsText(fixture, input, new WordsEditRequest
        {
            OutputPath = fixture.Temp.File("fields.txt"),
            Verify = true,
        });

        Assert.Equal("FIELD_COUNT_CHANGED", issue.Code);
        // Evaluation mode adds a field of its own to every document it loads, so only the
        // loss is fixed, not the counts.
        System.Text.RegularExpressions.Match counts = System.Text.RegularExpressions.Regex.Match(
            issue.Message, @"expected (\d+), found (\d+)");
        Assert.True(counts.Success, issue.Message);
        Assert.True(int.Parse(counts.Groups[2].Value) < int.Parse(counts.Groups[1].Value), issue.Message);
        Assert.Equal("txt may not keep fields; save to docx or another Word format and verify again.", issue.Hint);
        Assert.Null(issue.Location);
    }

    [Fact]
    public void Verify_ReportsRevisionsLostOnReopen()
    {
        using var fixture = new WordsFixture();
        VerificationIssue issue = VerifyAsText(fixture, fixture.CreateReport(), new WordsEditRequest
        {
            OutputPath = fixture.Temp.File("revisions.txt"),
            Verify = true,
            TrackChanges = true,
            Author = "Reviewer",
        });

        Assert.Equal("REVISION_COUNT_CHANGED", issue.Code);
        Assert.Contains("found 0", issue.Message, StringComparison.Ordinal);
        Assert.Contains("save to docx", issue.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_ReportsProtectionLostOnReopen()
    {
        using var fixture = new WordsFixture();
        WordsEditResult result = fixture.Engine.ApplyOps(
            fixture.CreateReport(),
            new WordsOpsBatch { Ops = [new ProtectOp { Mode = "readOnly" }] },
            new WordsEditRequest { OutputPath = fixture.Temp.File("protection.txt"), Verify = true });

        Assert.False(result.Verification!.Ok);
        Assert.True(result.HasFailures);
        VerificationIssue issue = Assert.Single(result.Verification.Issues);
        Assert.Equal("PROTECTION_CHANGED", issue.Code);
        Assert.Contains("expected readOnly, found none", issue.Message, StringComparison.Ordinal);
        Assert.Contains("save to docx", issue.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    /// No realistic edit loses this state through a Word-format save, so the hint is checked
    /// directly: advising a Word format again would send the caller round the same loop.
    /// </summary>
    [Theory]
    [InlineData("docx")]
    [InlineData("doc")]
    [InlineData("flatopc")]
    public void Hint_ForAWordFormatOutput_DoesNotAdviseSavingToAWordFormat(string format)
    {
        string hint = WordsMutationService.KeepStateHint("fields", format, WordsFormats.WordIds);

        Assert.DoesNotContain("save to", hint, StringComparison.Ordinal);
        Assert.Contains($"did not survive save and reopen in {format}", hint, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("odt")]
    [InlineData("rtf")]
    public void Hint_ForRevisionsInAFormatThatStoresThem_DoesNotAdviseAnotherFormat(string format)
    {
        string hint = WordsMutationService.KeepStateHint("tracked revisions", format, WordsFormats.RevisionIds);

        Assert.DoesNotContain("save to", hint, StringComparison.Ordinal);
    }

    [Fact]
    public void VerificationCodes_AreDeclaredVerificationDiagnostics()
    {
        foreach (string code in new[] { "FIELD_COUNT_CHANGED", "REVISION_COUNT_CHANGED", "PROTECTION_CHANGED" })
        {
            Assert.Contains(WordsDiagnostics.All, descriptor =>
                descriptor.Code == code && descriptor.Category == "verification");
        }
    }

    private static VerificationIssue VerifyAsText(WordsFixture fixture, string input, WordsEditRequest request)
    {
        WordsEditResult result = fixture.Engine.ApplyOps(
            input,
            new WordsOpsBatch { Ops = [new ReplaceTextOp { Find = "twelve", Replace = "ten" }] },
            request);

        Assert.Equal("ok", Assert.Single(result.Applied).Status);
        Assert.False(result.Verification!.Ok);
        return Assert.Single(result.Verification.Issues);
    }
}
