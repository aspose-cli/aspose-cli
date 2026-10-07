using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Invariants;

/// <summary>
/// The declarative scenarios in <c>Invariants/Scenarios</c> run against the built CLI, and the
/// scenario format rejects documents it cannot run as written.
/// </summary>
public sealed class ScenarioTests
{
    private static readonly string Directory =
        Path.Combine(RepositoryPaths.Root, "tests", "Aspose.Cli.Platform.Tests", "Invariants", "Scenarios");

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
