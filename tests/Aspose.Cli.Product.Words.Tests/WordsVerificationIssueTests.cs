using Aspose.Cli.Sdk.Contracts;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// The checks a verified edit runs on the reopened output. OpenDocument text keeps fields and
/// revisions but not editing restrictions, so a verified edit saved as <c>.odt</c> loses the
/// protection the check compares.
/// </summary>
public sealed class WordsVerificationIssueTests
{
    [Fact]
    public void Verify_ReportsProtectionLostOnReopen()
    {
        using var fixture = new WordsFixture();
        WordsEditResult result = WordsEdit.Run(
            fixture.Session,
            new WordsEditRequest
            {
                Input = fixture.CreateReport(),
                Batch = new WordsOpsBatch { Ops = [new ProtectDocumentOp { Protection = "read-only" }] },
                Output = TestOutput.At(fixture.Temp.File("protection.odt")),
                Verify = true,
            });

        Assert.False(result.Verification!.Ok);
        Assert.True(result.HasFailures);
        VerificationIssue issue = Assert.Single(result.Verification.Issues);
        Assert.Equal("PROTECTION_MISMATCH", issue.Code);
        Assert.Contains("expected read-only, found none", issue.Message, StringComparison.Ordinal);
        Assert.Equal("odt may not keep protection; save to docx or another Word format and verify again.", issue.Hint);
    }

    [Fact]
    public void Verify_ChecksTheOperationsWhoseFieldsProtectionOrRevisionsItCompares()
    {
        using var fixture = new WordsFixture();
        WordsEditResult result = WordsEdit.Run(
            fixture.Session,
            new WordsEditRequest
            {
                Input = fixture.CreateReport(),
                Batch = new WordsOpsBatch { Ops = [new ReplaceTextOp { Find = "twelve", Replace = "ten" }, new ProtectDocumentOp { Id = "lock", Protection = "read-only" }] },
                Output = TestOutput.At(fixture.Temp.File("checked.docx")),
                Verify = true,
            });

        Assert.True(result.Verification!.Ok);
        Assert.Equal(["lock"], result.Verification.CheckedOps);
    }

    [Fact]
    public void Verify_WithTrackedChanges_ChecksEveryAppliedOperation()
    {
        using var fixture = new WordsFixture();
        WordsEditResult result = WordsEdit.Run(
            fixture.Session,
            new WordsEditRequest
            {
                Input = fixture.CreateReport(),
                Batch = ReplaceTwelve,
                Output = TestOutput.At(fixture.Temp.File("checked-tracked.docx")),
                Verify = true,
                TrackChanges = true,
                Author = "Reviewer",
            });

        Assert.Equal(["op-0001"], result.Verification!.CheckedOps);
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
        string hint = WordsEdit.KeepStateHint("fields", format, WordsFormats.WordIds);

        Assert.DoesNotContain("save to", hint, StringComparison.Ordinal);
        Assert.Contains($"did not survive save and reopen in {format}", hint, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("odt")]
    [InlineData("rtf")]
    public void Hint_ForRevisionsInAFormatThatStoresThem_DoesNotAdviseAnotherFormat(string format)
    {
        string hint = WordsEdit.KeepStateHint("tracked revisions", format, WordsFormats.RevisionIds);

        Assert.DoesNotContain("save to", hint, StringComparison.Ordinal);
    }

    [Fact]
    public void VerificationCodes_AreDeclaredVerificationDiagnostics()
    {
        foreach (string code in new[] { "FIELD_COUNT_MISMATCH", "REVISION_COUNT_MISMATCH", "PROTECTION_MISMATCH" })
        {
            Assert.Contains(WordsDiagnostics.All, descriptor =>
                descriptor.Code == code && descriptor.Category == "verification");
        }
    }

    private static readonly WordsOpsBatch ReplaceTwelve = new() { Ops = [new ReplaceTextOp { Find = "twelve", Replace = "ten" }] };
}
