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

    /// <summary>
    /// A step that passes <c>--verify</c> states the exit code it ends with and whether its
    /// verification passed (<c>verification.ok</c> with <c>equals</c>), so an edit whose
    /// verification fails is never recorded as passing by a step that asserts something else.
    /// </summary>
    [Fact]
    public void VerifyingSteps_StateTheirExitCodeAndVerificationOk()
    {
        string[] unstated = [.. System.IO.Directory.EnumerateFiles(Directory, "*.scenario.json").Order(StringComparer.Ordinal)
            .SelectMany(static path => Scenario.Load(path).Steps.Select((step, index) => (File: Path.GetFileName(path), Number: index + 1, Step: step)))
            .Where(static item => item.Step.Args.Any(static arg => arg == "--verify" || arg.StartsWith("--verify=", StringComparison.Ordinal))
                && (item.Step.Expect?.ExitCode is null
                    || item.Step.Expect.Json?.Any(static assertion => assertion.Path == "verification.ok" && assertion.Value is not null) != true))
            .Select(static item => $"{item.File} step {item.Number}")];

        Assert.True(unstated.Length == 0, "Steps that pass --verify without expect.exitCode and a verification.ok equals assertion: " + string.Join(", ", unstated));
    }

    [Fact]
    public void FailedExpectations_AreAllReported()
    {
        Scenario scenario = Scenario.Parse("""
            {
              "name": "expectations that do not hold",
              "files": { "notes.txt": { "text": "Hello" } },
              "steps": [
                {
                  "args": ["words", "read", "missing.docx"],
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
                      { "path": "error.code", "startsWith": "USAGE_" },
                      { "path": "error.details.available", "notContains": "--verbose" },
                      { "path": "error.message", "notContains": "--verbose" },
                      { "path": "error.details.errors", "notContains": "anything" },
                      { "path": "error.code", "startsWith": "Run" },
                      { "path": "error.details.available", "notContains": "cells" },
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
            ["error.code", "error.details.available", "error.message", "error.details", "error.details.missing"],
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
                  "args": ["words", "read", "sub/missing.docx"],
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

    /// <summary>
    /// A warning selector names a code the CLI warns: a misspelt code, or an error's, would keep no
    /// warning and so hold any <c>exists: false</c>, and the scenario fails as it loads.
    /// </summary>
    [Theory]
    [InlineData("LOSSY_CONVERSON")]
    [InlineData("FILE_NOT_FOUND")]
    public void WarningSelector_OfACodeTheCliDoesNotWarn_IsRejected(string code)
    {
        InvalidDataException rejected = Assert.Throws<InvalidDataException>(() => Scenario.Parse($$$"""
            {"name":"a selector of a code no warning has","files":{"brief.md":{"text":"# Brief"}},
             "steps":[{"args":["words","create","brief.docx"],
               "expect":{"exitCode":0,"json":[{"path":"warnings[code={{{code}}}][0]","exists":false}]}}]}
            """));

        Assert.Contains(code, rejected.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("error.details.available[1]", true, "\"b\"")]
    [InlineData("error.details.available[2]", false, null)]
    [InlineData("error.code", true, "\"USAGE_ERROR\"")]
    [InlineData("error.missing", false, null)]
    [InlineData("warnings[code=LOSSY_CONVERSION][1].details.content[0]", true, "\"pictures\"")]
    [InlineData("warnings[code=LOSSY_CONVERSION][2]", false, null)]
    [InlineData("warnings[code=FONTS_MISSING][0]", false, null)]
    [InlineData("error.details.available[code=a]", false, null)]
    [InlineData("warnings[code=ENCRYPTION_REMOVED]", true, """[{"code":"ENCRYPTION_REMOVED"}]""")]
    public void JsonPath_ResolvesNamesAndIndexes(string path, bool found, string? expected)
    {
        JsonNode document = JsonNode.Parse("""
            {"error":{"code":"USAGE_ERROR","details":{"available":["a","b"]}},
             "warnings":[{"code":"ENCRYPTION_REMOVED"},{"code":"LOSSY_CONVERSION"},{"code":"LOSSY_CONVERSION","details":{"content":["pictures"]}}]}
            """)!;

        Assert.Equal(found, ScenarioRunner.TryResolve(document, path, out JsonNode? value));
        Assert.Equal(expected, value?.ToJsonString());
    }

    /// <summary>
    /// <c>only</c> holds for exactly the listed codes, and fails on another code, or one more or
    /// one fewer of the same.
    /// </summary>
    [Theory]
    [InlineData("LOSSY_CONVERSION LOSSY_CONVERSION", "LOSSY_CONVERSION LOSSY_CONVERSION", true)]
    [InlineData("LOSSY_CONVERSION LOSSY_CONVERSION", "LOSSY_CONVERSION LOSSY_CONVERSION FONTS_SUBSTITUTED", false)]
    [InlineData("LOSSY_CONVERSION LOSSY_CONVERSION", "LOSSY_CONVERSION LOSSY_CONVERSION LOSSY_CONVERSION", false)]
    [InlineData("LOSSY_CONVERSION LOSSY_CONVERSION", "LOSSY_CONVERSION", false)]
    [InlineData("", "", true)]
    [InlineData("", "FONTS_SUBSTITUTED", false)]
    public void OnlyCodes_AreEveryCode(string expected, string warned, bool holds)
    {
        JsonArray warnings = [.. warned.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(static code => new JsonObject { ["code"] = code })];
        var codes = new ScenarioCodes { Only = expected.Split(' ', StringSplitOptions.RemoveEmptyEntries) };

        Assert.Equal(holds, !ScenarioRunner.WarningProblems(codes, warnings).Any());
    }
}
