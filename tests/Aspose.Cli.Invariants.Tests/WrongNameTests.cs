using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// The wrong names callers have written, in <c>wrong-names.json</c>: each gets the suggestion its
/// row states, and the request corrected as the suggestion says succeeds, so one error is all it
/// takes. A wrong name no name stands for gets no suggestion, since a guess misleads more than the
/// list of names the error already gives. Every row is one scenario, so it carries the scenario
/// contract checks too.
/// </summary>
public sealed class WrongNameTests
{
    private const string FileName = "wrong-names.json";

    // The smallest picture every product can place.
    private const string Picture = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";

    private static readonly Dictionary<string, string> Documents = new(StringComparer.Ordinal)
    {
        ["words"] = "a.docx",
        ["cells"] = "b.xlsx",
        ["pdf"] = "c.pdf",
        ["slides"] = "d.pptx",
    };

    private static readonly Lazy<IReadOnlyList<WrongName>> Rows = new(static () => Read());

    public static TheoryData<string> Names() => [.. Rows.Value.Select(static row => row.Id)];

    [Theory]
    [MemberData(nameof(Names))]
    public void AWrongName_GetsItsSuggestion_AndTheSuggestionWorks(string name) =>
        ScenarioRunner.Run(Rows.Value.Single(row => row.Id == name).ToScenario()).AssertPassed();

    /// <summary>A row that does not say what it means proves nothing, or something else than it says.</summary>
    [Fact]
    public void EveryRow_IsARequestWithOneWrongName_AndSaysWhatItGets()
    {
        string[] problems = [.. Rows.Value.SelectMany(static row => row.Problems())];

        Assert.True(problems.Length == 0, string.Join(Environment.NewLine, problems));
        Assert.Equal(Rows.Value.Count, Rows.Value.Select(static row => row.Id).Distinct(StringComparer.Ordinal).Count());
    }

    private static IReadOnlyList<WrongName> Read()
    {
        string text = File.ReadAllText(Path.Combine(RepositoryPaths.Root, "tests", "Aspose.Cli.Invariants.Tests", FileName));
        JsonNode list = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip })!;
        return [.. list.AsArray().Select(static row => WrongName.Of(row!.AsObject()))];
    }

    /// <summary>One row: an edit's operations or a command line, the name in it that is wrong, and what the error must say.</summary>
    private sealed record WrongName(
        string? Product,
        JsonNode? Setup,
        JsonNode? Operations,
        string? CommandLine,
        string Wrong,
        string? Suggests,
        string? Because,
        string? Hint,
        JsonNode? Fixed)
    {
        public static WrongName Of(JsonObject row) => new(
            row["edit"]?.GetValue<string>(),
            row["first"],
            row["ops"],
            row["run"]?.GetValue<string>(),
            row["wrong"]!.GetValue<string>(),
            row["suggests"]?.GetValue<string>(),
            row["because"]?.GetValue<string>(),
            row["hint"]?.GetValue<string>(),
            row["fixed"]);

        public string Id => Operations switch
        {
            null => CommandLine!,
            JsonArray operations => $"{Product} {operations.Select(Holding).OfType<JsonObject>().Select(static op => op["op"]!.GetValue<string>()).FirstOrDefault()}: {Wrong}",
            _ => $"{Product} document: {Wrong}",
        };

        private JsonNode? Holding(JsonNode? op) => Occurrences(op) > 0 ? op : null;

        public Scenario ToScenario()
        {
            var steps = new List<ScenarioStep>();
            if (Setup is not null)
            {
                steps.Add(Step(Setup is JsonArray ? EditArguments(Setup, "--in-place") : Setup.GetValue<string>().Split(' '), new ScenarioExpectation { ExitCode = [0] }));
            }

            steps.Add(Step(Arguments(Operations, CommandLine), new ScenarioExpectation
            {
                Error = Operations is null ? ["USAGE_ERROR", "OPTION_INVALID"] : ["OPS_INVALID"],
                Json = Assertions(),
                Files = new ScenarioFiles { NothingWritten = true },
            }));
            if (Corrected() is { } corrected)
            {
                steps.Add(Step(corrected, new ScenarioExpectation { ExitCode = [0] }));
            }

            return new Scenario { Name = $"the wrong name of '{Id}'", Files = Workspace(), Steps = steps };
        }

        public IEnumerable<string> Problems()
        {
            if (Operations is null == CommandLine is null || (Operations is not null && (Product is null || !Documents.ContainsKey(Product))))
            {
                yield return $"{Id}: a row is one edit of a known product or one command line";
            }

            int found = Operations is null ? Positions().Count() : Occurrences(Operations);
            if (found == 0 || (found > 1 && Fixed is null))
            {
                yield return $"{Id}: '{Wrong}' is written {found} times; a row without 'fixed' renames the one place it is written";
            }

            if ((Suggests is null) == string.IsNullOrWhiteSpace(Because))
            {
                yield return $"{Id}: 'because' says why a row has no suggestion, and only such a row has it";
            }
        }

        private ScenarioStep Step(string[] arguments, ScenarioExpectation expectation) => new()
        {
            Args = arguments,
            Env = new Dictionary<string, string>(StringComparer.Ordinal) { ["ROW_PASSWORD"] = "Row-Password-5e1b" },
            Expect = expectation,
        };

        private IReadOnlyList<JsonAssertion> Assertions()
        {
            var assertions = new List<JsonAssertion>();
            if (Suggests is null)
            {
                assertions.Add(new JsonAssertion { Path = "error.details.suggestions", Exists = false });
            }
            else
            {
                assertions.Add(new JsonAssertion { Path = "error.details.suggestions[0]", Value = Suggests });
                assertions.Add(new JsonAssertion { Path = "error.hint", Contains = Suggests });
            }

            if (Hint is not null)
            {
                assertions.Add(new JsonAssertion { Path = "error.hint", Contains = Hint });
            }

            return assertions;
        }

        // The request to run after the error: the row's own, else the one with the name replaced
        // by the suggestion; nothing when neither says what works.
        private string[]? Corrected()
        {
            if (Fixed is not null)
            {
                return Fixed is JsonValue ? Arguments(null, Fixed.GetValue<string>()) : Arguments(Fixed, null);
            }

            if (Suggests is null)
            {
                return null;
            }

            if (Operations is not null)
            {
                return Arguments(Renamed(Operations), null);
            }

            string[] tokens = CommandLine!.Split(' ');
            int at = Positions().First();
            return [.. tokens[..at], .. Suggests.Split(' '), .. tokens[(at + Wrong.Split(' ').Length)..]];
        }

        private string[] Arguments(JsonNode? operations, string? commandLine) =>
            operations is null ? commandLine!.Split(' ') : EditArguments(operations, "--out", "out" + Path.GetExtension(Documents[Product!]));

        private string[] EditArguments(JsonNode operations, params string[] output) =>
            [Product!, "edit", Documents[Product!], "--ops", operations.ToJsonString(), .. output];

        private IEnumerable<int> Positions()
        {
            string[] tokens = CommandLine!.Split(' ');
            string[] wrong = Wrong.Split(' ');
            return Enumerable.Range(0, Math.Max(0, tokens.Length - wrong.Length + 1))
                .Where(at => tokens.Skip(at).Take(wrong.Length).SequenceEqual(wrong, StringComparer.Ordinal));
        }

        private int Occurrences(JsonNode? node) => node switch
        {
            JsonObject members => members.Sum(member => (member.Key == Wrong ? 1 : 0) + Occurrences(member.Value)),
            JsonArray items => items.Sum(Occurrences),
            JsonValue value when value.TryGetValue(out string? text) && text == Wrong => 1,
            _ => 0,
        };

        private JsonNode Renamed(JsonNode node) => node switch
        {
            JsonObject members => new JsonObject(members.Select(member =>
                KeyValuePair.Create(member.Key == Wrong ? Suggests! : member.Key, member.Value is null ? null : Renamed(member.Value)))),
            JsonArray items => new JsonArray([.. items.Select(item => item is null ? null : Renamed(item))]),
            JsonValue value when value.TryGetValue(out string? text) && text == Wrong => JsonValue.Create(Suggests!),
            _ => node.DeepClone(),
        };

        private static IReadOnlyDictionary<string, ScenarioFile> Workspace() => new Dictionary<string, ScenarioFile>(StringComparer.Ordinal)
        {
            ["a.docx"] = new() { Fixture = "words.docx" },
            ["a2.docx"] = new() { Fixture = "words.docx" },
            ["b.xlsx"] = new() { Fixture = "cells.xlsx" },
            ["b2.xlsx"] = new() { Fixture = "cells.xlsx" },
            ["c.pdf"] = new() { Fixture = "pdf.pdf" },
            ["c2.pdf"] = new() { Fixture = "pdf.pdf" },
            ["m.pdf"] = new() { Fixture = "pdf.media" },
            ["d.pptx"] = new() { Fixture = "slides.pptx" },
            ["d2.pptx"] = new() { Fixture = "slides.pptx" },
            ["in.md"] = new() { Fixture = "markdown" },
            ["pic.png"] = new() { Base64 = Picture },
        };
    }
}
