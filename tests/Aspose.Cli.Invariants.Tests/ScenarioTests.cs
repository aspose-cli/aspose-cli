using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// The declarative scenarios in <c>Scenarios</c> run against the built CLI, and the
/// scenario format rejects documents it cannot run as written.
/// </summary>
public sealed class ScenarioTests
{
    private static readonly string Directory =
        Path.Combine(RepositoryPaths.Root, "tests", "Aspose.Cli.Invariants.Tests", "Scenarios");

    public static TheoryData<string> Files() =>
        [.. System.IO.Directory.EnumerateFiles(Directory, "*.scenario.json").Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(Files))]
    public void ScenarioPasses(string file) =>
        ScenarioRunner.Run(Path.Combine(Directory, file)).AssertPassed();

    [Fact]
    public void FailedExpectations_AreAllReported()
    {
        Scenario scenario = Scenario.Parse("""
            {
              "name": "expectations that do not hold",
              "files": { "notes.txt": { "text": "Hello" } },
              "steps": [
                {
                  "args": ["words", "inspect", "missing.docx"],
                  "expect": {
                    "exitCode": 0,
                    "error": "FILE_CORRUPT",
                    "json": [{ "path": "error.details.path", "exists": false }],
                    "files": { "present": ["missing.docx"], "absent": ["notes.txt"] }
                  }
                }
              ]
            }
            """);

        ScenarioOutcome outcome = ScenarioRunner.Run(scenario);

        Assert.Equal(
            [ScenarioRunner.ExitCodeCheck, ScenarioRunner.ErrorCheck, ScenarioRunner.JsonCheck, ScenarioRunner.FilesCheck, ScenarioRunner.FilesCheck],
            outcome.Problems.Select(static problem => problem.Check));
        Assert.All(outcome.Problems, static problem => Assert.DoesNotContain(Path.GetTempPath(), problem.Message, StringComparison.OrdinalIgnoreCase));
        Assert.Contains("FILE_NOT_FOUND", outcome.Problems[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StartsWithAndNotContains_JudgeStringsAndArrays()
    {
        Scenario scenario = Scenario.Parse("""
            {
              "name": "prefix and absence assertions",
              "steps": [
                {
                  "args": ["--vresion"],
                  "expect": {
                    "json": [
                      { "path": "error.hint", "startsWith": "Did you mean '--version'?" },
                      { "path": "error.details.suggestions", "notContains": "--verbose" },
                      { "path": "error.message", "notContains": "--verbose" },
                      { "path": "error.details.errors", "notContains": "anything" },
                      { "path": "error.hint", "startsWith": "Run" },
                      { "path": "error.details.suggestions", "notContains": "--version" },
                      { "path": "error.message", "notContains": "vresion" },
                      { "path": "error.details", "notContains": "--version" },
                      { "path": "error.details.missing", "startsWith": "x" }
                    ]
                  }
                }
              ]
            }
            """);

        ScenarioOutcome outcome = ScenarioRunner.Run(scenario);

        Assert.Equal(
            ["error.hint", "error.details.suggestions", "error.message", "error.details", "error.details.missing"],
            outcome.Problems.Select(static problem => problem.Message.Split(' ')[0]));
        Assert.All(outcome.Problems, static problem => Assert.Equal(ScenarioRunner.JsonCheck, problem.Check));
    }

    [Fact]
    public void EqualsWorkspace_StandsForTheWorkspaceDirectory()
    {
        Scenario scenario = Scenario.Parse("""
            {
              "name": "a path under the workspace",
              "steps": [
                {
                  "args": ["words", "inspect", "sub/missing.docx"],
                  "expect": {
                    "error": "FILE_NOT_FOUND",
                    "json": [
                      { "path": "error.details.path", "equals": "<workspace>/sub/missing.docx" },
                      { "path": "error.details.path", "equals": "<workspace>/other.docx" }
                    ]
                  }
                }
              ]
            }
            """);

        ScenarioOutcome outcome = ScenarioRunner.Run(scenario);

        ScenarioProblem problem = Assert.Single(outcome.Problems);
        Assert.Equal(ScenarioRunner.JsonCheck, problem.Check);
        Assert.Contains("other.docx", problem.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"name":"x","steps":[{"args":["doctor"],"expectation":{}}]}""")]
    [InlineData("""{"name":"x","files":{"a.txt":{"text":"a","base64":"YQ=="}},"steps":[{"args":["doctor"]}]}""")]
    [InlineData("""{"name":"x","files":{"a.txt":{}},"steps":[{"args":["doctor"]}]}""")]
    [InlineData("""{"name":"x","steps":[]}""")]
    public void InvalidScenario_IsRejected(string json) =>
        Assert.ThrowsAny<Exception>(() => Scenario.Parse(json));

    [Theory]
    [InlineData("error.details.suggestions[1]", true, "\"b\"")]
    [InlineData("error.details.suggestions[2]", false, null)]
    [InlineData("error.code", true, "\"USAGE_ERROR\"")]
    [InlineData("error.missing", false, null)]
    public void JsonPath_ResolvesNamesAndIndexes(string path, bool found, string? expected)
    {
        JsonNode document = JsonNode.Parse("""{"error":{"code":"USAGE_ERROR","details":{"suggestions":["a","b"]}}}""")!;

        Assert.Equal(found, ScenarioRunner.TryResolve(document, path, out JsonNode? value));
        Assert.Equal(expected, value?.ToJsonString());
    }
}
