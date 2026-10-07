using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Aspose.Cli.TestKit.Scenarios;

/// <summary>
/// A declarative CLI scenario: the files a fresh workspace starts with, then the command lines
/// run in it, each with what it must produce. <c>README.md</c> in this folder documents the
/// JSON form that <see cref="Load"/> and <see cref="Parse"/> read.
/// </summary>
public sealed record Scenario
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>What the scenario shows, in one line.</summary>
    public required string Name { get; init; }

    /// <summary>The files to create before the first step, by workspace-relative path.</summary>
    public IReadOnlyDictionary<string, ScenarioFile> Files { get; init; } = new Dictionary<string, ScenarioFile>();

    /// <summary>The command lines to run in order.</summary>
    public required IReadOnlyList<ScenarioStep> Steps { get; init; }

    /// <summary>The directory that <see cref="ScenarioFile.Copy"/> paths are relative to; the scenario file's own directory.</summary>
    [JsonIgnore]
    public string? BaseDirectory { get; init; }

    /// <summary>Reads a scenario file.</summary>
    public static Scenario Load(string path) =>
        Parse(File.ReadAllText(path), System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)));

    /// <summary>Reads a scenario document; unknown fields are rejected.</summary>
    public static Scenario Parse(string json, string? baseDirectory = null)
    {
        Scenario scenario = JsonSerializer.Deserialize<Scenario>(json, Options)
            ?? throw new InvalidDataException("A scenario document is null.");
        if (scenario.Steps.Count == 0)
        {
            throw new InvalidDataException($"Scenario '{scenario.Name}' has no steps.");
        }
        foreach ((string path, ScenarioFile file) in scenario.Files)
        {
            int sources = new object?[] { file.Fixture, file.Text, file.Base64, file.Copy }.Count(static source => source is not null);
            if (sources != 1)
            {
                throw new InvalidDataException($"Scenario file '{path}' must name exactly one of fixture, text, base64 or copy.");
            }
        }
        return scenario with { BaseDirectory = baseDirectory };
    }
}

/// <summary>One seed file; exactly one source is set.</summary>
public sealed record ScenarioFile
{
    /// <summary>A named document from <see cref="ScenarioFixtures"/>, such as <c>cells.xlsx</c>.</summary>
    public string? Fixture { get; init; }

    /// <summary>UTF-8 text without a byte order mark.</summary>
    public string? Text { get; init; }

    /// <summary>Raw bytes in Base64.</summary>
    public string? Base64 { get; init; }

    /// <summary>A file to copy, relative to the scenario file.</summary>
    public string? Copy { get; init; }
}

/// <summary>One CLI invocation.</summary>
public sealed record ScenarioStep
{
    /// <summary>
    /// The arguments after <c>aspose-cli</c>. <c>--output json</c> is appended unless they already
    /// choose an output with <c>--output</c> or <c>-f</c>.
    /// </summary>
    public required IReadOnlyList<string> Args { get; init; }

    /// <summary>Extra environment variables for this invocation.</summary>
    public IReadOnlyDictionary<string, string>? Env { get; init; }

    /// <summary>The standard input; empty when omitted.</summary>
    public string? Stdin { get; init; }

    /// <summary>What the invocation must produce; the contract checks apply either way.</summary>
    public ScenarioExpectation? Expect { get; init; }
}

/// <summary>What one step must produce. Every field is optional.</summary>
public sealed record ScenarioExpectation
{
    /// <summary>The exit code, or any of several.</summary>
    [JsonConverter(typeof(OneOrManyConverter<int>))]
    public IReadOnlyList<int>? ExitCode { get; init; }

    /// <summary>The error code of the failure envelope, or any of several.</summary>
    [JsonConverter(typeof(OneOrManyConverter<string>))]
    public IReadOnlyList<string>? Error { get; init; }

    /// <summary>Warning codes that the result must or must not carry.</summary>
    public ScenarioCodes? Warnings { get; init; }

    /// <summary>Assertions on the JSON document the step wrote: the result, or the error envelope.</summary>
    public IReadOnlyList<JsonAssertion>? Json { get; init; }

    /// <summary>Expectations on the workspace after the step.</summary>
    public ScenarioFiles? Files { get; init; }

    /// <summary>
    /// Files that must open again: each is inspected with the product routing assigns to it, else
    /// the command's own product when it reads it, or checked for its file signature otherwise. <c>product:path</c> names the product;
    /// <c>@result</c> stands for every file the result lists in <c>output.path</c>,
    /// <c>outputs[].path</c> or, for a render, <c>outputs[].output.path</c>.
    /// </summary>
    public IReadOnlyList<string>? Reopens { get; init; }

    /// <summary>Texts, such as secret values, that must not appear in stdout or stderr.</summary>
    public IReadOnlyList<string>? Hidden { get; init; }
}

/// <summary>Codes that must be present and codes that must be absent.</summary>
public sealed record ScenarioCodes
{
    public IReadOnlyList<string>? Present { get; init; }

    public IReadOnlyList<string>? Absent { get; init; }
}

/// <summary>Workspace expectations; paths are workspace-relative.</summary>
public sealed record ScenarioFiles
{
    /// <summary>Files that must exist.</summary>
    public IReadOnlyList<string>? Present { get; init; }

    /// <summary>Files that must not exist.</summary>
    public IReadOnlyList<string>? Absent { get; init; }

    /// <summary>Files whose bytes must equal what they were before the step.</summary>
    public IReadOnlyList<string>? Unchanged { get; init; }

    /// <summary>When true, the step creates, changes and deletes no file at all.</summary>
    public bool? NothingWritten { get; init; }
}

/// <summary>
/// One assertion on a JSON value addressed by a dotted path with indexes, such as
/// <c>error.details.suggestions</c> or <c>outputs[0].format</c>.
/// </summary>
public sealed record JsonAssertion
{
    public required string Path { get; init; }

    /// <summary>The value must equal this JSON value.</summary>
    [JsonPropertyName("equals")]
    public JsonNode? Value { get; init; }

    /// <summary>The array must hold this value, or the string must contain this text.</summary>
    public JsonNode? Contains { get; init; }

    /// <summary>Whether the value must exist (true) or be missing (false).</summary>
    public bool? Exists { get; init; }
}

/// <summary>Reads a single value or an array of values as a list.</summary>
internal sealed class OneOrManyConverter<T> : JsonConverter<IReadOnlyList<T>>
{
    public override IReadOnlyList<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.StartArray
            ? JsonSerializer.Deserialize<T[]>(ref reader, options)!
            : [JsonSerializer.Deserialize<T>(ref reader, options)!];

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<T> value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value.ToArray(), options);
}
