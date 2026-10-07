using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xunit;

namespace Aspose.Cli.TestKit.Scenarios;

/// <summary>
/// Runs a <see cref="Scenario"/> against the built CLI in a fresh <see cref="TempWorkspace"/>
/// under the run's license mode (<see cref="ScenarioLicense"/>) and reports every unmet
/// expectation and contract check (<see cref="ScenarioContract"/>) as a problem, rather than
/// stopping at the first one.
/// </summary>
public static partial class ScenarioRunner
{
    /// <summary>The step's exit code is not an expected one.</summary>
    public const string ExitCodeCheck = "exit-code";

    /// <summary>The step's error code is not an expected one.</summary>
    public const string ErrorCheck = "error";

    /// <summary>A warning code is missing or present against the expectation.</summary>
    public const string WarningsCheck = "warnings";

    /// <summary>A JSON assertion does not hold.</summary>
    public const string JsonCheck = "json";

    /// <summary>A workspace expectation does not hold.</summary>
    public const string FilesCheck = "files";

    /// <summary>An output does not open again.</summary>
    public const string ReopensCheck = "reopens";

    /// <summary>A hidden text appears in stdout or stderr.</summary>
    public const string HiddenCheck = "hidden";

    private const string ResultOutputs = "@result";

    /// <summary>Reads and runs a scenario file.</summary>
    public static ScenarioOutcome Run(string path) => Run(Scenario.Load(path));

    /// <summary>Runs a scenario; the workspace is removed afterwards.</summary>
    public static ScenarioOutcome Run(Scenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        using var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        Seed(scenario, workspace);
        var problems = new List<ScenarioProblem>();
        for (int index = 0; index < scenario.Steps.Count; index++)
        {
            ScenarioStep step = scenario.Steps[index];
            int number = index + 1;
            // Messages name the workspace, not its random path, and replace generated ids, such as
            // a temporary directory's suffix, so a problem reads the same in every run.
            void Report(string check, string message) => problems.Add(new ScenarioProblem(number, check, GeneratedId().Replace(message
                .Replace(workspace.Path.Replace("\\", "\\\\", StringComparison.Ordinal), "<workspace>", StringComparison.OrdinalIgnoreCase)
                .Replace(workspace.Path, "<workspace>", StringComparison.OrdinalIgnoreCase), "<id>")));

            IReadOnlyDictionary<string, string> before = Snapshot(workspace.Path);
            string[] args = WithJsonOutput(step.Args);
            CliResult result = Execute(workspace, args, step.Env, step.Stdin);
            IReadOnlyDictionary<string, string> after = Snapshot(workspace.Path);
            JsonNode? document = ScenarioContract.Check(args, result, Report);
            if (step.Expect is { } expect)
            {
                CheckExpectation(expect, workspace, args, result, document, before, after, Report);
            }
        }
        return new ScenarioOutcome(scenario.Name, problems);
    }

    private static void Seed(Scenario scenario, TempWorkspace workspace)
    {
        foreach ((string relative, ScenarioFile file) in scenario.Files)
        {
            string target = workspace.File(relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            byte[] bytes = file switch
            {
                { Fixture: { } fixture } => ScenarioFixtures.Read(fixture),
                { Text: { } text } => Encoding.UTF8.GetBytes(text),
                { Base64: { } base64 } => Convert.FromBase64String(base64),
                { Copy: { } copy } => File.ReadAllBytes(Path.Combine(
                    scenario.BaseDirectory ?? throw new InvalidOperationException($"Scenario '{scenario.Name}' copies '{copy}' but has no base directory."),
                    copy)),
                _ => throw new InvalidOperationException($"Scenario file '{relative}' has no source."),
            };
            File.WriteAllBytes(target, bytes);
        }
    }

    private static CliResult Execute(
        TempWorkspace workspace,
        IReadOnlyList<string> args,
        IReadOnlyDictionary<string, string>? env,
        string? stdin)
    {
        Dictionary<string, string?>? variables = env?.ToDictionary(static pair => pair.Key, static pair => (string?)pair.Value);
        var environment = CliEnvironment.Evaluation(Path.GetDirectoryName(workspace.ConfigDirectory)!, variables);
        // Standard input is always redirected, so a command never waits on the test's console.
        return new CliProcess(CliRunner.ExecutablePath, environment).Run(workspace.Path, stdin ?? string.Empty, [.. args]);
    }

    private static string[] WithJsonOutput(IReadOnlyList<string> args) =>
        args.Any(static arg => arg is "--output" or "-f" || arg.StartsWith("--output=", StringComparison.Ordinal))
            ? [.. args]
            : [.. args, "--output", "json"];

    private static void CheckExpectation(
        ScenarioExpectation expect,
        TempWorkspace workspace,
        IReadOnlyList<string> args,
        CliResult result,
        JsonNode? document,
        IReadOnlyDictionary<string, string> before,
        IReadOnlyDictionary<string, string> after,
        Action<string, string> report)
    {
        string? error = document?["error"]?["code"]?.GetValue<string>();
        if (expect.ExitCode is { } exitCodes && !exitCodes.Contains(result.ExitCode))
        {
            report(ExitCodeCheck, $"exit {result.ExitCode}{(error is null ? string.Empty : " " + error)}, expected {string.Join(" or ", exitCodes)}"
                + (result.ExitCode == 0 ? string.Empty : ": " + Message(document, result)));
        }
        if (expect.Error is { } errors && (error is null || !errors.Contains(error)))
        {
            report(ErrorCheck, $"{(error is null ? $"no error (exit {result.ExitCode})" : $"{error}: {Message(document, result)}")}, expected {string.Join(" or ", errors)}");
        }
        if (expect.Warnings is { } warnings)
        {
            string[] codes = [.. ScenarioContract.Codes(document?["warnings"])];
            foreach (string missing in (warnings.Present ?? []).Where(code => !codes.Contains(code)))
            {
                report(WarningsCheck, $"warning {missing} is missing; the result warns [{string.Join(", ", codes)}]");
            }
            foreach (string present in (warnings.Absent ?? []).Where(code => codes.Contains(code)))
            {
                report(WarningsCheck, $"warning {present} is present");
            }
        }
        foreach (JsonAssertion assertion in expect.Json ?? [])
        {
            if (Evaluate(document, assertion) is { } failure)
            {
                report(JsonCheck, failure);
            }
        }
        if (expect.Files is { } files)
        {
            CheckFiles(files, before, after, report);
        }
        foreach (string reopen in expect.Reopens ?? [])
        {
            Reopen(workspace, reopen, args, document, report);
        }
        foreach (string hidden in expect.Hidden ?? [])
        {
            if (hidden.Length > 0 && (result.StdOut.Contains(hidden, StringComparison.Ordinal) || result.StdErr.Contains(hidden, StringComparison.Ordinal)))
            {
                report(HiddenCheck, $"the hidden text appears in {(result.StdOut.Contains(hidden, StringComparison.Ordinal) ? "stdout" : "stderr")}");
            }
        }
    }

    private static string Message(JsonNode? document, CliResult result) =>
        document?["error"]?["message"]?.GetValue<string>() is { } message
            ? ScenarioContract.Excerpt(message)
            : ScenarioContract.Excerpt(result.StdErr);

    private static void CheckFiles(
        ScenarioFiles files,
        IReadOnlyDictionary<string, string> before,
        IReadOnlyDictionary<string, string> after,
        Action<string, string> report)
    {
        foreach (string path in files.Present ?? [])
        {
            if (!after.ContainsKey(Normalize(path)))
            {
                report(FilesCheck, $"{path} does not exist");
            }
        }
        foreach (string path in files.Absent ?? [])
        {
            if (after.ContainsKey(Normalize(path)))
            {
                report(FilesCheck, $"{path} exists");
            }
        }
        foreach (string path in files.Unchanged ?? [])
        {
            string key = Normalize(path);
            if (!before.TryGetValue(key, out string? hash) || !after.TryGetValue(key, out string? now) || hash != now)
            {
                report(FilesCheck, $"{path} changed");
            }
        }
        if (files.NothingWritten == true)
        {
            string[] written =
            [
                .. after.Where(pair => !before.TryGetValue(pair.Key, out string? hash) || hash != pair.Value).Select(static pair => pair.Key),
                .. before.Keys.Where(key => !after.ContainsKey(key)),
            ];
            if (written.Length > 0)
            {
                report(FilesCheck, $"wrote {string.Join(", ", written.Order(StringComparer.Ordinal).Take(5))}");
            }
        }
    }

    private static void Reopen(TempWorkspace workspace, string entry, IReadOnlyList<string> args, JsonNode? document, Action<string, string> report)
    {
        CliCatalog catalog = CliCatalog.Current;
        string? product = null;
        string target = entry;
        int colon = entry.IndexOf(':', StringComparison.Ordinal);
        if (colon > 0 && catalog.Products.Any(candidate => candidate.Id == entry[..colon]))
        {
            product = entry[..colon];
            target = entry[(colon + 1)..];
        }
        string[] paths = target == ResultOutputs
            ? [.. OutputPaths(document)]
            : [workspace.File(target)];
        if (paths.Length == 0)
        {
            report(ReopensCheck, "the result lists no output");
        }
        string? preferred = args.Count > 0 && catalog.Products.Any(candidate => candidate.Id == args[0]) ? args[0] : null;
        foreach (string path in paths)
        {
            string name = Path.GetFileName(path);
            if (!File.Exists(path))
            {
                report(ReopensCheck, $"{name} does not exist");
                continue;
            }
            string? reader = product ?? catalog.ReaderOf(path, preferred);
            if (reader is null)
            {
                if (SignatureProblem(path) is { } problem)
                {
                    report(ReopensCheck, $"{name} {problem}");
                }
                continue;
            }
            CliResult inspect = Execute(workspace, [reader, "inspect", path, "--output", "json"], env: null, stdin: null);
            if (inspect.ExitCode != 0)
            {
                JsonNode? failure = ErrorOf(inspect.StdErr);
                report(ReopensCheck, $"{reader} inspect {name} failed: exit {inspect.ExitCode} "
                    + (failure is null
                        ? ScenarioContract.Excerpt(inspect.StdErr)
                        : $"{failure["code"]?.GetValue<string>()}: {ScenarioContract.Excerpt(failure["message"]?.GetValue<string>() ?? string.Empty)}"));
            }
        }
    }

    private static JsonNode? ErrorOf(string stderr)
    {
        try
        {
            return JsonNode.Parse(stderr)?["error"];
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>Every file a result lists in <c>output.path</c>, <c>outputs[].path</c> or <c>outputs[].output.path</c>.</summary>
    private static IEnumerable<string> OutputPaths(JsonNode? document)
    {
        if (document?["output"]?["path"]?.GetValue<string>() is { } path)
        {
            yield return path;
        }
        foreach (JsonNode? output in document?["outputs"] as JsonArray ?? [])
        {
            // A convert lists each file itself; a render lists each page with its file.
            if ((output?["path"] ?? output?["output"]?["path"])?.GetValue<string>() is { } item)
            {
                yield return item;
            }
        }
    }

    /// <summary>A file no product reopens must be non-empty and, for a well-known format, carry its signature.</summary>
    private static string? SignatureProblem(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length == 0)
        {
            return "is empty";
        }
        static bool StartsWith(byte[] bytes, params byte[] prefix) => bytes.AsSpan().StartsWith(prefix);
        string extension = Path.GetExtension(path).ToLowerInvariant();
        bool valid = extension switch
        {
            ".png" => StartsWith(bytes, 0x89, 0x50, 0x4E, 0x47),
            ".jpg" or ".jpeg" => StartsWith(bytes, 0xFF, 0xD8, 0xFF),
            ".gif" => StartsWith(bytes, 0x47, 0x49, 0x46, 0x38),
            ".tif" or ".tiff" => StartsWith(bytes, 0x49, 0x49, 0x2A, 0x00) || StartsWith(bytes, 0x4D, 0x4D, 0x00, 0x2A),
            ".pdf" => StartsWith(bytes, 0x25, 0x50, 0x44, 0x46),
            ".ps" => StartsWith(bytes, 0x25, 0x21),
            ".xps" or ".oxps" or ".epub" => StartsWith(bytes, 0x50, 0x4B, 0x03, 0x04),
            ".svg" => Encoding.UTF8.GetString(bytes).Contains("<svg", StringComparison.Ordinal),
            ".html" or ".htm" => Encoding.UTF8.GetString(bytes).Contains("<html", StringComparison.OrdinalIgnoreCase),
            _ => true,
        };
        return valid ? null : $"does not carry the {extension} signature";
    }

    private static string? Evaluate(JsonNode? document, JsonAssertion assertion)
    {
        bool found = TryResolve(document, assertion.Path, out JsonNode? value);
        if (assertion.Exists is { } exists && exists != found)
        {
            return exists ? $"{assertion.Path} is missing" : $"{assertion.Path} exists: {Show(value)}";
        }
        if (assertion.Value is { } expected && (!found || !JsonNode.DeepEquals(value, expected)))
        {
            return $"{assertion.Path} is {(found ? Show(value) : "missing")}, expected {Show(expected)}";
        }
        if (assertion.Contains is { } item)
        {
            bool contains = value switch
            {
                JsonArray array => array.Any(element => JsonNode.DeepEquals(element, item)),
                JsonValue text when text.TryGetValue(out string? actual) && item is JsonValue part && part.TryGetValue(out string? wanted)
                    => actual.Contains(wanted, StringComparison.Ordinal),
                _ => false,
            };
            if (!contains)
            {
                return $"{assertion.Path} is {(found ? Show(value) : "missing")}, expected it to contain {Show(item)}";
            }
        }
        return null;
    }

    private static string Show(JsonNode? value) => ScenarioContract.Excerpt(value?.ToJsonString() ?? "null");

    /// <summary>Resolves <c>a.b[0].c</c>; true when every segment exists.</summary>
    internal static bool TryResolve(JsonNode? root, string path, out JsonNode? value)
    {
        value = root;
        foreach (string segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            int bracket = segment.IndexOf('[', StringComparison.Ordinal);
            string name = bracket < 0 ? segment : segment[..bracket];
            if (name.Length > 0)
            {
                if (value is not JsonObject obj || !obj.TryGetPropertyValue(name, out value))
                {
                    value = null;
                    return false;
                }
            }
            for (int open = bracket; open >= 0; open = segment.IndexOf('[', open + 1))
            {
                int close = segment.IndexOf(']', open);
                int index = int.Parse(segment[(open + 1)..close], NumberStyles.None, CultureInfo.InvariantCulture);
                if (value is not JsonArray array || index >= array.Count)
                {
                    value = null;
                    return false;
                }
                value = array[index];
            }
        }
        return true;
    }

    /// <summary>Content hashes of the workspace by relative path, without the project license directory.</summary>
    private static IReadOnlyDictionary<string, string> Snapshot(string root)
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            string relative = Normalize(Path.GetRelativePath(root, path));
            if (!relative.StartsWith(ScenarioLicense.ProjectDirectory + "/", StringComparison.OrdinalIgnoreCase))
            {
                files[relative] = FileHashes.Sha256(path);
            }
        }
        return files;
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    /// <summary>A generated id: sixteen or more lower-case hexadecimal digits and dashes with a digit among them, such as a GUID.</summary>
    [GeneratedRegex("(?=[0-9a-f-]*[0-9])[0-9a-f][0-9a-f-]{14,}[0-9a-f]")]
    private static partial Regex GeneratedId();
}

/// <summary>What one scenario run produced.</summary>
public sealed record ScenarioOutcome(string Name, IReadOnlyList<ScenarioProblem> Problems)
{
    /// <summary>Whether every expectation and contract check held.</summary>
    public bool Passed => Problems.Count == 0;

    /// <summary>Fails the test with every problem when the scenario did not pass.</summary>
    public void AssertPassed() => Assert.True(Passed, ToString());

    public override string ToString() =>
        Passed
            ? $"Scenario '{Name}' passed."
            : $"Scenario '{Name}' failed:\n" + string.Join('\n', Problems.Select(static problem => "  " + problem));
}

/// <summary>One unmet expectation or contract check of a step (1-based).</summary>
public sealed record ScenarioProblem(int Step, string Check, string Message)
{
    public override string ToString() => $"step {Step} [{Check}] {Message}";
}
