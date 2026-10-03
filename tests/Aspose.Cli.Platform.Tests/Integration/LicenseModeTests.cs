using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// <c>--license-mode evaluation</c> runs one command in evaluation mode whatever the license
/// configuration says: no source is read, so even a broken one cannot fail it, and the result
/// discloses evaluation mode as it does without a license.
/// </summary>
public sealed class LicenseModeTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public LicenseModeTests() =>
        File.WriteAllText(_workspace.File("brief.md"), "# Project delivery\n\nApproved client scope.\n");

    public void Dispose() => _workspace.Dispose();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EvaluationMode_ReadsNoSourceSoABrokenOneCannotFailIt(bool supervised)
    {
        ProjectLicense("<License>synthetic invalid fixture</License>"u8.ToArray());
        string[] timeout = supervised ? ["--timeout", "60"] : [];

        CliResult configured = _workspace.Run(
            ["words", "create", "configured.docx", "--markdown", "brief.md", "--output", "json", .. timeout]);
        Assert.Equal(7, configured.ExitCode);
        Assert.Equal("LICENSE_INVALID", Error(configured)["code"]!.GetValue<string>());

        JsonNode requested = Create("requested.docx", timeout);
        Assert.Equal("evaluation", requested["license"]!["mode"]!.GetValue<string>());
        Assert.Contains(requested["warnings"]!.AsArray(), warning => warning!["code"]!.GetValue<string>() == "EVAL_MODE");

        JsonNode status = _workspace.Run(
            "license", "status", "--product", "words", "--license-mode", "evaluation", "--output", "json").Json();
        JsonNode words = Assert.Single(status["products"]!.AsArray())!;
        Assert.Equal("evaluation", words["mode"]!.GetValue<string>());
        Assert.Equal("requested", words["source"]!.GetValue<string>());
        Assert.Null(words["path"]);
    }

    [LicensedFact]
    public void EvaluationMode_DisclosesEvaluationOutputDespiteAValidLicense()
    {
        ProjectLicense(File.ReadAllBytes(TestLicense.Path!));

        JsonNode licensed = _workspace.Run(
            "words", "create", "licensed.docx", "--markdown", "brief.md", "--output", "json").Json();
        JsonNode requested = Create("requested.docx", []);

        Assert.Equal("licensed", licensed["license"]!["mode"]!.GetValue<string>());
        Assert.DoesNotContain(
            licensed["warnings"]?.AsArray() ?? [], warning => warning!["code"]!.GetValue<string>() == "EVAL_MODE");
        Assert.Equal("evaluation", requested["license"]!["mode"]!.GetValue<string>());
        Assert.Contains(requested["warnings"]!.AsArray(), warning => warning!["code"]!.GetValue<string>() == "EVAL_MODE");
    }

    [Fact]
    public void Doctor_SaysTheEvaluationModeWasRequested()
    {
        JsonNode doctor = _workspace.Run("doctor", "--license-mode", "evaluation", "--output", "json").Json();

        JsonNode license = Assert.Single(
            doctor["checks"]!.AsArray(), check => check!["name"]!.GetValue<string>() == "license")!;
        Assert.Equal("warn", license["status"]!.GetValue<string>());
        Assert.Contains("--license-mode evaluation", license["detail"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void EvaluationMode_CannotBeCombinedWithAnExplicitLicense()
    {
        CliResult result = _workspace.Run(
            "words", "create", "brief.docx", "--markdown", "brief.md",
            "--license", "any.lic", "--license-mode", "evaluation", "--output", "json");

        Assert.Equal(2, result.ExitCode);
        JsonNode error = Error(result);
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal("--license-mode", error["details"]!["option"]!.GetValue<string>());
        Assert.False(File.Exists(_workspace.File("brief.docx")));
    }

    [Theory]
    [InlineData("license", "install", "any.lic")]
    [InlineData("license", "remove")]
    [InlineData("app", "--no-open")]
    public void EvaluationMode_IsRefusedByCommandsWhoseEffectOutlivesThem(params string[] command)
    {
        // The refusal needs no file, so a missing one is not reported first.
        CliResult result = _workspace.Run([.. command, "--license-mode", "evaluation", "--output", "json"]);

        Assert.Equal(2, result.ExitCode);
        JsonNode error = Error(result);
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal("--license-mode", error["details"]!["option"]!.GetValue<string>());
    }

    private JsonNode Create(string output, string[] options)
    {
        CliResult result = _workspace.Run(
            ["words", "create", output, "--markdown", "brief.md", "--license-mode", "evaluation", "--output", "json", .. options]);
        Assert.True(result.ExitCode == 0, result.StdErr);
        JsonNode created = JsonNode.Parse(result.StdOut)!;
        // The disclosure says the evaluation was asked for, not that a license is missing.
        JsonNode warning = Assert.Single(
            created["warnings"]!.AsArray(), warning => warning!["code"]!.GetValue<string>() == "EVAL_MODE")!;
        Assert.Contains("--license-mode evaluation", warning["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("--license-mode evaluation", warning["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.DoesNotContain("ASPOSE_LICENSE_PATH", warning["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        return created;
    }

    private void ProjectLicense(byte[] content)
    {
        string path = _workspace.File(Path.Combine(".aspose", "license.lic"));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    private static JsonNode Error(CliResult result) => JsonNode.Parse(result.StdErr)!["error"]!;
}
