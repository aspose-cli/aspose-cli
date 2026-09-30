using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Serialization;
using Xunit;

namespace Aspose.Cli.Sdk.Tests;

public sealed class VerificationIssueTests
{
    private static readonly DiagnosticDescriptor CountChanged =
        DiagnosticDescriptor.Verification("TEST_COUNT_CHANGED", "test");

    [Fact]
    public void Verification_DeclaresAVerificationCategoryWarning()
    {
        Assert.Equal("TEST_COUNT_CHANGED", CountChanged.Code);
        Assert.Equal("test", CountChanged.Owner);
        Assert.Equal(DiagnosticSeverity.Warning, CountChanged.Severity);
        Assert.Null(CountChanged.ExitCode);
        Assert.Equal(DiagnosticDescriptor.VerificationCategory, CountChanged.Category);
    }

    [Fact]
    public void Of_TakesTheCodeFromTheDescriptor()
    {
        VerificationIssue issue = VerificationIssue.Of(
            CountChanged, "The count changed.", "sheet:Data", "Inspect the sheet.");

        Assert.Equal("TEST_COUNT_CHANGED", issue.Code);
        Assert.Equal("The count changed.", issue.Message);
        Assert.Equal("sheet:Data", issue.Location);
        Assert.Equal("Inspect the sheet.", issue.Hint);
    }

    [Fact]
    public void Of_RejectsADescriptorOutsideTheVerificationCategory()
    {
        DiagnosticDescriptor warning = DiagnosticDescriptor.Warning("TEST_WARNING", "test", "warning");

        Assert.Throws<ArgumentException>(() => VerificationIssue.Of(warning, "Not a verification code."));
    }

    [Fact]
    public void From_KeepsTheWarningsLocationAndHint()
    {
        var warning = new Warning
        {
            Code = "TEST_WARNING",
            Message = "A bounded warning.",
            Hint = "Inspect the affected item.",
            Docs = "test/verification",
            Location = "page:2",
            AffectsCompleteness = true,
        };

        VerificationIssue issue = VerificationIssue.From(warning);

        Assert.Equal(
            new VerificationIssue
            {
                Code = "TEST_WARNING",
                Message = "A bounded warning.",
                Location = "page:2",
                Hint = "Inspect the affected item.",
            },
            issue);
    }

    [Fact]
    public void OptionalFields_AreOmittedWhenAbsent()
    {
        JsonObject json = JsonSerializer.SerializeToNode(
            VerificationIssue.Of(CountChanged, "The count changed."),
            SdkJsonContext.Default.VerificationIssue)!.AsObject();

        Assert.Equal(["code", "message"], json.Select(static item => item.Key));
    }
}
