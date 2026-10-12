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
        Run("words", "create", "brief.docx", "--from-markdown", "brief.md", "--license-mode", "evaluation");

        JsonNode printed = Run("review", "brief.docx", "--out", "review", "--license-mode", "evaluation");

        string[] codes = Codes(printed);
        Assert.Contains("EVALUATION_MODE", codes);
        Assert.Equal(codes, Codes(JsonNode.Parse(File.ReadAllText(_workspace.File(Path.Combine("review", "review.json"))))!));
        Assert.Contains("\"EVALUATION_MODE\"", File.ReadAllText(_workspace.File(Path.Combine("review", "index.html"))), StringComparison.Ordinal);
    }

    [Fact]
    public void EvaluationReview_ListsTheMarksItsEvidenceShows_AsRenderDoes()
    {
        Run("words", "create", "brief.docx", "--from-markdown", "brief.md", "--license-mode", "evaluation");

        JsonNode rendered = Run("words", "render", "brief.docx", "--out", "page.png", "--license-mode", "evaluation");
        JsonNode reviewed = Run("review", "brief.docx", "--out", "review", "--license-mode", "evaluation");

        string expected = Message(rendered, "EVALUATION_MODE");
        Assert.Contains("The rendered source carries", expected, StringComparison.Ordinal);
        Assert.Equal(expected, Message(reviewed, "EVALUATION_MODE"));
    }

    [LicensedFact]
    public void LicensedReviewOfAMarkedDocument_WritesWhatItPrints()
    {
        ScenarioLicense.Project(_workspace.Path);
        Run("words", "create", "marked.docx", "--from-markdown", "brief.md", "--license-mode", "evaluation");

        JsonNode printed = Run("review", "marked.docx", "--out", "review");

        // A licensed result omits its license block.
        Assert.Null(printed["license"]);
        Assert.DoesNotContain("EVALUATION_MODE", Codes(printed));
        Assert.Equal(Codes(printed), Codes(JsonNode.Parse(File.ReadAllText(_workspace.File(Path.Combine("review", "review.json"))))!));
        Assert.Contains(printed["findings"]!.AsArray(), static finding => finding!["code"]!.GetValue<string>() == "WORDS_EVALUATION_MARKS");
        // The evidence images show the marks the input carries, as a licensed render of it says.
        JsonNode rendered = Run("words", "render", "marked.docx", "--out", "page.png");
        Assert.Equal(Message(rendered, "EVALUATION_INPUT_MARKED"), Message(printed, "EVALUATION_INPUT_MARKED"));
    }

    private JsonNode Run(params string[] args)
    {
        CliResult result = _workspace.Run([.. args, "--output-format", "json"]);
        Assert.True(result.ExitCode == 0, result.StdErr);
        return JsonNode.Parse(result.StdOut)!;
    }

    private static string Message(JsonNode result, string code) =>
        Assert.Single(result["warnings"] as JsonArray ?? [], warning => warning!["code"]!.GetValue<string>() == code)!["message"]!.GetValue<string>();

    private static string[] Codes(JsonNode result) =>
        [.. (result["warnings"] as JsonArray ?? []).Select(static warning => warning!["code"]!.GetValue<string>())];
}
