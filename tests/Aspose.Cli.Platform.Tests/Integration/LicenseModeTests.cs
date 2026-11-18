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

    [Fact]
    public void EvaluationMode_ReadsNoSourceSoABrokenOneCannotFailIt()
    {
        ProjectLicense("<License>synthetic invalid fixture</License>"u8.ToArray());

        CliResult configured = _workspace.Run(
            ["words", "create", "configured.docx", "--from-markdown", "brief.md", "--output-format", "json"]);
        Assert.Equal(7, configured.ExitCode);
        Assert.Equal("LICENSE_INVALID", Error(configured)["code"]!.GetValue<string>());

        JsonNode requested = Create("requested.docx");
        Assert.Equal("evaluation", requested["license"]!["mode"]!.GetValue<string>());
        Assert.Contains(requested["warnings"]!.AsArray(), warning => warning!["code"]!.GetValue<string>() == "EVALUATION_MODE");

        JsonNode status = _workspace.Run(
            "license", "status", "--product", "words", "--license-mode", "evaluation", "--output-format", "json").Json();
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
            "words", "create", "licensed.docx", "--from-markdown", "brief.md", "--output-format", "json").Json();
        JsonNode requested = Create("requested.docx");

        Assert.Null(licensed["license"]);
        Assert.DoesNotContain(
            licensed["warnings"]?.AsArray() ?? [], warning => warning!["code"]!.GetValue<string>() == "EVALUATION_MODE");
        Assert.Equal("evaluation", requested["license"]!["mode"]!.GetValue<string>());
        Assert.Contains(requested["warnings"]!.AsArray(), warning => warning!["code"]!.GetValue<string>() == "EVALUATION_MODE");
    }

    private JsonNode Create(string output)
    {
        CliResult result = _workspace.Run(
            ["words", "create", output, "--from-markdown", "brief.md", "--license-mode", "evaluation", "--output-format", "json"]);
        Assert.True(result.ExitCode == 0, result.StdErr);
        JsonNode created = JsonNode.Parse(result.StdOut)!;
        // The disclosure says the evaluation was asked for, not that a license is missing.
        JsonNode warning = Assert.Single(
            created["warnings"]!.AsArray(), warning => warning!["code"]!.GetValue<string>() == "EVALUATION_MODE")!;
        Assert.Contains("--license-mode evaluation", warning["message"]!.GetValue<string>(), StringComparison.Ordinal);
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
