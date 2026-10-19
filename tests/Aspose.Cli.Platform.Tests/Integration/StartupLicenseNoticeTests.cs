using System.Text.Json.Nodes;
using Aspose.Cli.IntegrationTests;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

public sealed class StartupLicenseNoticeTests
{
    [Fact]
    public void RootHelp_HumanOutputReportsEveryNativeEngineOnStderr()
    {
        using var workspace = new TempWorkspace();
        // The notice does not depend on which human format is chosen; the other tests use table.
        CliResult result = workspace.Run("--help", "--output-format", "markdown");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage:", result.StdOut);
        AssertAllEvaluation(result.StdErr);
        Assert.DoesNotContain("license:", result.StdOut);
    }

    [Theory]
    [MemberData(nameof(CliContractTests.ProductIds), MemberType = typeof(CliContractTests))]
    public void ProductHelp_ReportsOnlyTheSelectedNativeEngine(string product)
    {
        using var workspace = new TempWorkspace();
        CliResult result = workspace.Run(product, "--help", "--output-format", "table");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal($"license: {product}=evaluation", result.StdErr.Trim());
        Assert.Contains("Usage:", result.StdOut);
    }

    [LicensedFact]
    public void LicensedRun_PrintsNoNotice()
    {
        using var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        CliResult result = workspace.Run("--help", "--output-format", "table");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage:", result.StdOut);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public void ExistingInvalidLicense_IsRejectedByTheNativeEngineBeforeReportingStatus()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("invalid.lic"), "<License>not a signed Aspose license</License>");
        CliResult result = workspace.Run("words", "--help", "--license", "invalid.lic", "--output-format", "table");

        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith("license: words=invalid", result.StdErr);
        Assert.Contains("license status", result.StdErr);
        Assert.DoesNotContain("words=licensed", result.StdErr);
        Assert.DoesNotContain("not a signed Aspose license", result.StdErr);
        Assert.Contains("Usage:", result.StdOut);
    }

    [Fact]
    public void MissingExplicitLicense_ReportsInvalidWithoutHidingHelp()
    {
        using var workspace = new TempWorkspace();
        CliResult result = workspace.Run("words", "--help", "--license", "missing.lic", "--output-format", "table");

        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith("license: words=invalid", result.StdErr);
        Assert.DoesNotContain("words=licensed", result.StdErr);
        Assert.Contains("Usage:", result.StdOut);
    }

    [Fact]
    public void RequestedEvaluation_IsMarkedAsRequested()
    {
        using var workspace = new TempWorkspace();
        CliResult result = workspace.Run("words", "--help", "--license-mode", "evaluation", "--output-format", "table");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("license: words=evaluation (requested)", result.StdErr.Trim());
    }

    [Fact]
    public void Quiet_SuppressesTheStartupNotice()
    {
        using var workspace = new TempWorkspace();
        CliResult result = workspace.Run("--help", "--output-format", "table", "--quiet");

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.StdErr);
        Assert.Contains("Usage:", result.StdOut);
    }

    [Fact]
    public void Json_SuccessAndFailureKeepTheirSingleDocumentContracts()
    {
        using var workspace = new TempWorkspace();
        CliResult success = workspace.Run("capabilities", "--output-format", "json");
        Assert.Equal(0, success.ExitCode);
        Assert.Empty(success.StdErr);
        Assert.NotNull(JsonNode.Parse(success.StdOut)!["schema"]);

        CliResult failure = workspace.Run("unknown-startup-command", "--output-format", "json");
        Assert.NotEqual(0, failure.ExitCode);
        Assert.Empty(failure.StdOut);
        Assert.Equal("USAGE_ERROR", JsonNode.Parse(failure.StdErr)!["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void RedirectedDefault_RemainsJsonWithoutStartupNoise()
    {
        using var workspace = new TempWorkspace();
        CliResult result = workspace.Run("capabilities");

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.StdErr);
        Assert.NotNull(JsonNode.Parse(result.StdOut)!["schema"]);
    }

    [Fact]
    public void RawSchema_HumanModeKeepsStdoutParseable()
    {
        using var workspace = new TempWorkspace();
        CliResult result = workspace.Run("schema", "v2/common/license-status", "--output-format", "table");

        Assert.Equal(0, result.ExitCode);
        Assert.NotNull(JsonNode.Parse(result.StdOut)!["$schema"]);
        AssertAllEvaluation(result.StdErr);
    }

    [Fact]
    public void Verbose_HumanModePreservesJsonlDiagnostics()
    {
        using var workspace = new TempWorkspace();
        CliResult result = workspace.Run("capabilities", "--output-format", "table", "--verbose");

        Assert.Equal(0, result.ExitCode);
        string line = Assert.Single(Lines(result.StdErr));
        Assert.Equal("completed", JsonNode.Parse(line)!["event"]!.GetValue<string>());
    }

    [Fact]
    public void SupervisedNativeCommand_ReportsTheLicenseOnlyOnce()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("source.md"), "# Project delivery\n\nApproved client scope.\n");
        CliResult result = workspace.Run("words", "create", "brief.docx", "--from-markdown", "source.md",
            "--timeout", TestDeadline.CliTimeout, "--output-format", "table");

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(workspace.File("brief.docx")));
        Assert.Equal("license: words=evaluation", Assert.Single(
            Lines(result.StdErr), static line => line.StartsWith("license:", StringComparison.Ordinal)));
        Assert.DoesNotContain("license:", result.StdOut);
    }

    [Theory]
    [InlineData("mcp", "serve")]
    [InlineData("__viewer-service")]
    [InlineData("__render-worker")]
    public void InternalAndProtocolEntrypoints_DoNotAddStartupNoise(params string[] entrypoint)
    {
        using var workspace = new TempWorkspace();
        CliResult result = workspace.Run([.. entrypoint, "--help", "--output-format", "table"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.StdErr);
        Assert.DoesNotContain("license:", result.StdOut);
    }

    private static void AssertAllEvaluation(string stderr)
    {
        string notice = Assert.Single(Lines(stderr));
        Assert.StartsWith("license: ", notice);
        string[] products = [.. CliCatalog.Current.Products.Select(static product => product.Id)];
        Assert.Equal(products.Length, notice["license: ".Length..].Split("; ").Length);
        foreach (string product in products)
        {
            Assert.Contains($"{product}=evaluation", notice);
        }
    }

    private static string[] Lines(string value) => value.Split(
        ['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
