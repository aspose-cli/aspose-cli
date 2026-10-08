using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// Review evidence shows what the engine draws, so the evaluation disclosure the command prints
/// is the one its review.json and index.html carry.
/// </summary>
public sealed class ReviewEvaluationDisclosureTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public ReviewEvaluationDisclosureTests() =>
        File.WriteAllText(_workspace.File("brief.md"), "# Project delivery\n\nApproved client scope.\n");

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void EvaluationReview_WritesTheDisclosureIntoTheEvidence()
    {
        Run("words", "create", "brief.docx", "--markdown", "brief.md", "--license-mode", "evaluation");

        JsonNode printed = Run("review", "brief.docx", "--out", "review", "--license-mode", "evaluation");

        string[] codes = Codes(printed);
        Assert.Contains("EVAL_MODE", codes);
        Assert.Equal(codes, Codes(JsonNode.Parse(File.ReadAllText(_workspace.File(Path.Combine("review", "review.json"))))!));
        Assert.Contains("\"EVAL_MODE\"", File.ReadAllText(_workspace.File(Path.Combine("review", "index.html"))), StringComparison.Ordinal);
    }

    [LicensedFact]
    public void LicensedReviewOfAMarkedDocument_WritesWhatItPrints()
    {
        ScenarioLicense.Project(_workspace.Path);
        Run("words", "create", "marked.docx", "--markdown", "brief.md", "--license-mode", "evaluation");

        JsonNode printed = Run("review", "marked.docx", "--out", "review");

        Assert.Equal("licensed", printed["license"]!["mode"]!.GetValue<string>());
        Assert.DoesNotContain("EVAL_MODE", Codes(printed));
        Assert.Equal(Codes(printed), Codes(JsonNode.Parse(File.ReadAllText(_workspace.File(Path.Combine("review", "review.json"))))!));
        Assert.Contains(printed["findings"]!.AsArray(), static finding => finding!["code"]!.GetValue<string>() == "WORDS_EVALUATION_MARKS");
    }

    private JsonNode Run(params string[] args)
    {
        CliResult result = _workspace.Run([.. args, "--output", "json"]);
        Assert.True(result.ExitCode == 0, result.StdErr);
        return JsonNode.Parse(result.StdOut)!;
    }

    private static string[] Codes(JsonNode result) =>
        [.. (result["warnings"] as JsonArray ?? []).Select(static warning => warning!["code"]!.GetValue<string>())];
}
