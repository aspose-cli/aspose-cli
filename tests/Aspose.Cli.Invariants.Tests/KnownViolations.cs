using System.Text.Json;
using System.Text.Json.Serialization;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// One recorded product defect: a generated case that violates an invariant today, in the
/// license modes listed (both when <see cref="Modes"/> is omitted). <see cref="Rule"/> is the
/// contract rule the invariant enforces (<see cref="InvariantCases.RuleOf"/>), and
/// <see cref="Cause"/> names the root cause in kebab case, so the entries one product fix removes
/// are listed together.
/// </summary>
internal sealed record KnownViolation(
    [property: JsonPropertyName("case")] string Case,
    [property: JsonPropertyName("invariant")] string Invariant,
    [property: JsonPropertyName("rule")] string Rule,
    [property: JsonPropertyName("cause")] string Cause,
    [property: JsonPropertyName("modes")] IReadOnlyList<string>? Modes,
    [property: JsonPropertyName("actual")] string Actual);

/// <summary>
/// The committed list of invariant violations the product has today
/// (<c>known-violations.json</c>). A case passes when its violations are exactly the listed ones
/// for the run's license mode, with the listed text, so a new or changed violation fails and so
/// does a listed one that now holds: the list only shrinks.
/// </summary>
internal static class KnownViolations
{
    public const string FileName = "known-violations.json";

    /// <summary>The pull-request label with which the owner accepts a new or wider entry (<c>.github/workflows/pull-request.yml</c>).</summary>
    public const string ApprovalLabel = "quality-exception";

    private static readonly Lazy<IReadOnlyList<KnownViolation>> Loaded = new(Load);

    /// <summary>
    /// A file to which every failing case appends its violations as entries in this list's
    /// format, one JSON object per line, with an empty cause to fill in: the way to draft the
    /// entries of a new rule's baseline.
    /// </summary>
    public const string DraftVariable = "ASPOSE_CLI_TEST_KNOWN_VIOLATIONS_DRAFT";

    private static readonly Lock DraftLock = new();

    private static readonly JsonSerializerOptions DraftOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string FilePath =>
        Path.Combine(RepositoryPaths.Root, "tests", "Aspose.Cli.Invariants.Tests", FileName);

    public static IReadOnlyList<KnownViolation> Entries => Loaded.Value;

    /// <summary>Runs one generated case and compares its violations with the list.</summary>
    public static void Verify(string id)
    {
        InvariantCase item = InvariantCases.All[id];
        IReadOnlyList<ScenarioProblem> problems = Problems(item);
        Dictionary<string, string> actual = problems
            .GroupBy(problem => InvariantOf(item, problem.Check), StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => Shorten(string.Join("; ", group.Select(static problem => problem.Message).Distinct())),
                StringComparer.Ordinal);
        string mode = ScenarioLicense.Mode;
        Dictionary<string, string> listed = Entries
            .Where(entry => entry.Case == id && Applies(entry, mode))
            .ToDictionary(static entry => entry.Invariant, static entry => entry.Actual, StringComparer.Ordinal);
        string[] added = [.. actual.Keys.Where(invariant => !listed.ContainsKey(invariant)).Order(StringComparer.Ordinal)];
        string[] changed = [.. actual.Keys.Where(invariant => listed.TryGetValue(invariant, out string? text) && text != actual[invariant]).Order(StringComparer.Ordinal)];
        string[] stale = [.. listed.Keys.Where(invariant => !actual.ContainsKey(invariant)).Order(StringComparer.Ordinal)];
        if (added.Length == 0 && changed.Length == 0 && stale.Length == 0)
        {
            return;
        }
        Draft(id, mode, [.. added, .. changed], actual);
        var message = new System.Text.StringBuilder();
        if (added.Length > 0)
        {
            message.AppendLine($"'{id}' breaks invariants in {mode} mode. Fix the product so the case holds:");
            foreach (string invariant in added)
            {
                // Every problem in full, one per line; the list compares the shortened text.
                message.AppendLine($"  {invariant}:");
                foreach (string text in problems.Where(problem => InvariantOf(item, problem.Check) == invariant)
                    .Select(static problem => problem.Message).Distinct())
                {
                    message.AppendLine($"    {text}");
                }
            }
            message.AppendLine($"  {FileName} lists only accepted product defects; a new entry needs the owner's approval, the '{ApprovalLabel}' label on the pull request.");
        }
        if (changed.Length > 0)
        {
            message.AppendLine($"'{id}' breaks listed invariants differently in {mode} mode. Fix the product so the case holds:");
            foreach (string invariant in changed)
            {
                message.AppendLine($"  {invariant}: {actual[invariant]}");
                message.AppendLine($"  {new string(' ', invariant.Length)}  listed: {listed[invariant]}");
            }
            message.AppendLine($"  Only if the new behavior is no worse, record its text in {FileName}; the pull request check shows the owner every reworded entry.");
        }
        if (stale.Length > 0)
        {
            message.AppendLine($"'{id}' now holds in {mode} mode for {string.Join(", ", stale)}; delete those entries from {FileName}.");
        }
        Assert.Fail(message.ToString());
    }

    /// <summary>
    /// Runs one case: a rule case's check, with the hints of its runs as problems of its invariant
    /// (<see cref="RuleWorkspace.CollectHintProblems"/>), or a scenario; a fixture that cannot be
    /// built is a problem of its own.
    /// </summary>
    internal static IReadOnlyList<ScenarioProblem> Problems(InvariantCase item)
    {
        try
        {
            return item.Check is { } check
                ? RuleWorkspace.CollectHintProblems(item.Invariant, check)
                : ScenarioRunner.Run(item.Scenario!).Problems;
        }
        catch (InvalidOperationException exception) when (exception.Message.StartsWith("A scenario fixture", StringComparison.Ordinal))
        {
            return [new ScenarioProblem(0, "seed", exception.Message)];
        }
    }

    /// <summary>The invariant a problem breaks: a contract check names its own, an expectation the case's.</summary>
    public static string InvariantOf(InvariantCase item, string check) =>
        check is ScenarioContract.NoInternalError or ScenarioContract.ErrorEnvelope or ScenarioContract.ResultEnvelope or ExecutableHints.Check
            ? check
            : item.Invariant;

    public static bool Applies(KnownViolation entry, string mode) =>
        entry.Modes is null || entry.Modes.Contains(mode, StringComparer.Ordinal);

    private static void Draft(string id, string mode, IEnumerable<string> invariants, Dictionary<string, string> actual)
    {
        if (Environment.GetEnvironmentVariable(DraftVariable) is not { Length: > 0 } path)
        {
            return;
        }
        string[] lines =
        [
            .. invariants.Select(invariant => JsonSerializer.Serialize(new KnownViolation(
                id, invariant, InvariantCases.RuleOf(invariant), string.Empty, [mode], actual[invariant]), DraftOptions)),
        ];
        lock (DraftLock)
        {
            File.AppendAllLines(path, lines);
        }
    }

    private static string Shorten(string text) => text.Length <= 240 ? text : text[..240] + "...";

    private static IReadOnlyList<KnownViolation> Load()
    {
        using FileStream stream = File.OpenRead(FilePath);
        using JsonDocument document = JsonDocument.Parse(stream);
        return document.RootElement.GetProperty("violations").Deserialize<KnownViolation[]>()
            ?? throw new InvalidDataException($"{FileName} has no violations array.");
    }
}
