using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>Helpers of the contract rules that run command lines (<c>InvocationRules.cs</c>).</summary>
internal static class InvocationChecks
{
    /// <summary>
    /// A case that runs its command lines in an <see cref="RuleWorkspace"/>: each message
    /// <paramref name="check"/> returns is one violation of <paramref name="rule"/>.
    /// </summary>
    public static InvariantCase Runs(
        string rule, string subject, string shard, bool slow, Invocation invocation, Func<RuleWorkspace, IEnumerable<string>> check) =>
        new($"{rule} | {subject}", rule, shard, slow, Scenario: null, Check: () =>
        {
            using var session = new RuleWorkspace(invocation);
            return [.. check(session).ToList().Select(message => new ScenarioProblem(0, rule, session.Clean(message)))];
        });

    /// <summary>The global option that chooses the output format: the root option that offers <c>json</c>.</summary>
    public static string OutputOption(CliCatalog catalog) =>
        catalog.Commands.Single(static command => command.Words.Length == 0).Options
            .First(static option => option.AllowedValues.Contains("json")).Name;

    /// <summary>An option's <c>inputKind</c>, which <see cref="CliOption"/> does not carry.</summary>
    public static string? InputKind(CliCatalog catalog, CliCommand command, string option) =>
        catalog.Document["commands"]!.AsArray()
            .Single(node => node!["path"]!.GetValue<string>() == (command.Words.Length == 0 ? "aspose-cli" : "aspose-cli " + command.Words))!
            ["options"]!.AsArray()
            .Single(node => node!["name"]!.GetValue<string>() == option)!
            ["inputKind"]?.GetValue<string>();

    /// <summary>The path of a subcommand, such as <c>cells convert</c>.</summary>
    public static string Join(string words, string name) => words.Length == 0 ? name : $"{words} {name}";

    /// <summary>The one JSON document a stream holds, or null.</summary>
    public static JsonNode? ParseJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The strings of a JSON array, or null when it is not one.</summary>
    public static string[]? Strings(JsonNode? node) =>
        node is JsonArray array ? [.. array.Select(static item => item?.GetValue<string>() ?? "null")] : null;

    /// <summary>One line of at most 160 characters, for a stable message.</summary>
    public static string Excerpt(string text)
    {
        string flat = text.ReplaceLineEndings(" ").Trim();
        return flat.Length == 0 ? "(empty)" : flat.Length <= 160 ? flat : flat[..160] + "...";
    }

    /// <summary>The lines of a stream without line endings or blank lines.</summary>
    public static string[] Lines(string text) =>
        [.. text.ReplaceLineEndings("\n").Split('\n').Where(static line => line.Trim().Length > 0)];

    /// <summary>Text with normalized line endings and no trailing white space.</summary>
    public static string Normalize(string text) => text.ReplaceLineEndings("\n").TrimEnd();
}
