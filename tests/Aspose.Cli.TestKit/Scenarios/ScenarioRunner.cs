using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xunit;

namespace Aspose.Cli.TestKit.Scenarios;

/// <summary>
/// Runs a <see cref="Scenario"/> against the built CLI in a fresh <see cref="TempWorkspace"/>
/// under the run's license mode (<see cref="ScenarioLicense"/>) and reports every unmet
/// expectation, contract check (<see cref="ScenarioContract"/>) and hint that does not run
/// (<see cref="ExecutableHints"/>) as a problem, rather than
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

    // Stands for the workspace directory in problems and at the start of an expected path.
    private const string WorkspacePlaceholder = "<workspace>";

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
                .Replace(workspace.Path.Replace("\\", "\\\\", StringComparison.Ordinal), WorkspacePlaceholder, StringComparison.OrdinalIgnoreCase)
                .Replace(workspace.Path, WorkspacePlaceholder, StringComparison.OrdinalIgnoreCase), "<id>")));

            bool installation = step.Expect?.Files?.NothingInstalled == true;
            IReadOnlyDictionary<string, string> before = Snapshot(workspace, installation);
            string[] args = WithJsonOutput(step.Args);
            bool marks = KeepsByteOrderMarks(workspace, args);
            CliResult result = Execute(workspace, args, step.Env, step.Stdin);
            IReadOnlyDictionary<string, string> after = Snapshot(workspace, installation);
            JsonNode? document = ScenarioContract.Check(args, result, Report);
            foreach (string written in after.Keys.Where(path => !marks && !path.StartsWith('<')
                && (!before.TryGetValue(path, out string? hash) || hash != after[path]) && StartsWithByteOrderMark(workspace.File(path))))
            {
                Report(ScenarioContract.ByteOrderMark, $"{written} starts with a UTF-8 byte order mark the step did not ask for and copied from no document");
            }
            foreach (string problem in ExecutableHints.Problems(new HintRun(args, workspace.Path, Path.GetDirectoryName(workspace.ConfigDirectory)!, Variables(step.Env)), document, variants: true))
            {
                Report(ExecutableHints.Check, problem);
            }
            foreach (string output in OutputPaths(document).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (FolderPathIn(output, workspace.Path) is { } where)
                {
                    Report(ScenarioContract.LocalPath, $"{Normalize(Path.GetRelativePath(workspace.Path, output))}{where} holds the absolute path of the folder of the step's inputs");
                }
            }
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
        var environment = CliEnvironment.Evaluation(Path.GetDirectoryName(workspace.ConfigDirectory)!, Variables(env));
        // Standard input is always redirected, so a command never waits on the test's console.
        return new CliProcess(CliRunner.ExecutablePath, environment).Run(workspace.Path, stdin ?? string.Empty, [.. args]);
    }

    private static Dictionary<string, string?>? Variables(IReadOnlyDictionary<string, string>? env) =>
        env?.ToDictionary(static pair => pair.Key, static pair => (string?)pair.Value);

    private static string[] WithJsonOutput(IReadOnlyList<string> args) =>
        args.Any(static arg => arg is "--output-format" || arg.StartsWith("--output-format=", StringComparison.Ordinal))
            ? [.. args]
            : [.. args, "--output-format", "json"];

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
        if (expect.RefusedOnlyWith is { } refusals && error is not null && !refusals.Contains(error))
        {
            report(ErrorCheck, $"{error}: {Message(document, result)}, expected success or {string.Join(" or ", refusals)}");
        }
        foreach (string problem in WarningProblems(expect.Warnings, document?["warnings"]))
        {
            report(WarningsCheck, problem);
        }
        foreach (JsonAssertion assertion in expect.Json ?? [])
        {
            if (Evaluate(document, assertion, workspace.Path) is { } failure)
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
            foreach (string written in Written(before, after).Where(static key => !key.StartsWith('<')))
            {
                if (hidden.Length > 0 && TextIn(workspace.File(written), hidden) is { } where)
                {
                    report(HiddenCheck, $"the hidden text appears in {written}{where}");
                }
            }
        }
    }

    /// <summary>The paths of two snapshots that a run created, changed or deleted, sorted.</summary>
    public static string[] Written(IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        return
        [
            .. after.Where(pair => !before.TryGetValue(pair.Key, out string? hash) || hash != pair.Value).Select(static pair => pair.Key)
                .Concat(before.Keys.Where(key => !after.ContainsKey(key)))
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// Where a file holds <paramref name="text"/> as UTF-8 or UTF-16 text, in its own bytes
    /// (an empty string) or in an entry of the ZIP package it is (<c> (entry name)</c>), such as a
    /// part of an Office document; null when it does not.
    /// </summary>
    public static string? TextIn(string path, string text)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(text);
        if (!File.Exists(path))
        {
            return null;
        }
        byte[] bytes = File.ReadAllBytes(path);
        if (Holds(bytes, text))
        {
            return string.Empty;
        }
        if (!bytes.AsSpan().StartsWith((ReadOnlySpan<byte>)[0x50, 0x4B, 0x03, 0x04]))
        {
            return null;
        }
        try
        {
            using var archive = new System.IO.Compression.ZipArchive(new MemoryStream(bytes), System.IO.Compression.ZipArchiveMode.Read);
            foreach (System.IO.Compression.ZipArchiveEntry entry in archive.Entries)
            {
                using Stream stream = entry.Open();
                using var content = new MemoryStream();
                stream.CopyTo(content);
                if (Holds(content.ToArray(), text))
                {
                    return $" ({entry.FullName})";
                }
            }
        }
        catch (InvalidDataException)
        {
            // Not a readable package; its own bytes were searched.
        }
        return null;
    }

    /// <summary>
    /// Where a file holds the absolute path of <paramref name="folder"/>, as <see cref="TextIn"/>
    /// finds a text, or null. The path is searched from the separator after its drive, whose letter
    /// a writer may change, with either separator and escaped as a file URI or a JSON string writes it.
    /// </summary>
    public static string? FolderPathIn(string path, string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        string tail = folder[Path.GetPathRoot(folder)!.TrimEnd('\\', '/').Length..];
        string escaped = new Uri(folder).AbsolutePath;
        string[] forms = [tail, tail.Replace('\\', '/'), escaped[escaped.IndexOf('/', 1)..], tail.Replace("\\", "\\\\", StringComparison.Ordinal)];
        return forms.Distinct(StringComparer.Ordinal).Select(form => TextIn(path, form)).FirstOrDefault(static where => where is not null);
    }

    private static bool Holds(byte[] bytes, string text) =>
        bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(text)) >= 0 || bytes.AsSpan().IndexOf(Encoding.Unicode.GetBytes(text)) >= 0;

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
        if (files.NothingWritten == true || files.NothingInstalled == true)
        {
            // Without nothingWritten, only the configuration and installation files are judged.
            bool Judged(string key) => files.NothingWritten == true
                || key.StartsWith(ConfigurationPrefix, StringComparison.Ordinal) || key.StartsWith(InstallationPrefix, StringComparison.Ordinal);
            string[] written = [.. Written(before, after).Where(Judged)];
            if (written.Length > 0)
            {
                report(FilesCheck, $"wrote {string.Join(", ", written.Take(5))}");
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
            CliResult inspect = Execute(workspace, [reader, "inspect", path, "--output-format", "json"], env: null, stdin: null);
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

    private static string? Evaluate(JsonNode? document, JsonAssertion assertion, string workspace)
    {
        bool found = TryResolve(document, assertion.Path, out JsonNode? value);
        if (assertion.Exists is { } exists && exists != found)
        {
            return exists ? $"{assertion.Path} is missing" : $"{assertion.Path} exists: {Show(value)}";
        }
        if (assertion.Value is { } expected && (!found || !JsonNode.DeepEquals(value, InWorkspace(expected, workspace))))
        {
            return $"{assertion.Path} is {(found ? Show(value) : "missing")}, expected {Show(expected)}";
        }
        if (assertion.Contains is { } item && !Holds(value, item))
        {
            return $"{assertion.Path} is {(found ? Show(value) : "missing")}, expected it to contain {Show(item)}";
        }
        if (assertion.NotContains is { } excluded && found)
        {
            if (value is not JsonArray && !(value is JsonValue text && text.TryGetValue(out string? _)))
            {
                return $"{assertion.Path} is {Show(value)}, expected an array or a string that does not contain {Show(excluded)}";
            }
            if (Holds(value, excluded))
            {
                return $"{assertion.Path} is {Show(value)}, expected it not to contain {Show(excluded)}";
            }
        }
        if (assertion.StartsWith is { } prefix
            && !(value is JsonValue start && start.TryGetValue(out string? begins) && begins.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return $"{assertion.Path} is {(found ? Show(value) : "missing")}, expected it to start with {Show(prefix)}";
        }
        return null;
    }

    /// <summary>
    /// The expected value with every string that starts with <c>&lt;workspace&gt;</c> made the path it
    /// names: the workspace directory, then the rest with <c>/</c> as the platform's separator.
    /// </summary>
    private static JsonNode? InWorkspace(JsonNode? expected, string workspace) => expected switch
    {
        JsonValue text when text.TryGetValue(out string? path) && path.StartsWith(WorkspacePlaceholder, StringComparison.Ordinal)
            => JsonValue.Create(workspace + path[WorkspacePlaceholder.Length..].Replace('/', Path.DirectorySeparatorChar)),
        JsonArray array => new JsonArray([.. array.Select(item => InWorkspace(item, workspace))]),
        JsonObject obj => new JsonObject(obj.Select(property => KeyValuePair.Create(property.Key, InWorkspace(property.Value, workspace)))),
        _ => expected?.DeepClone(),
    };

    /// <summary>Whether an array holds the item or a string contains the text.</summary>
    private static bool Holds(JsonNode? value, JsonNode item) => value switch
    {
        JsonArray array => array.Any(element => JsonNode.DeepEquals(element, item)),
        JsonValue text when text.TryGetValue(out string? actual) && item is JsonValue part && part.TryGetValue(out string? wanted)
            => actual.Contains(wanted, StringComparison.Ordinal),
        _ => false,
    };

    private static string Show(JsonNode? value) => ScenarioContract.Excerpt(value?.ToJsonString() ?? "null");

    /// <summary>What a result's warnings break of the codes a step expects of them.</summary>
    internal static IEnumerable<string> WarningProblems(ScenarioCodes? expected, JsonNode? warnings)
    {
        if (expected is null)
        {
            yield break;
        }
        string[] codes = [.. ScenarioContract.Codes(warnings)];
        foreach (string missing in (expected.Present ?? []).Where(code => !codes.Contains(code)))
        {
            yield return $"warning {missing} is missing; the result warns [{string.Join(", ", codes)}]";
        }
        foreach (string present in (expected.Absent ?? []).Where(code => codes.Contains(code)))
        {
            yield return $"warning {present} is present";
        }
        if (expected.Only is not { } only)
        {
            yield break;
        }
        string[] other = [.. codes.Where(static code => code != EvaluationMode).Order(StringComparer.Ordinal)];
        if (!other.SequenceEqual(only.Order(StringComparer.Ordinal)))
        {
            yield return $"the warnings other than {EvaluationMode} are [{string.Join(", ", other)}], expected only [{string.Join(", ", only)}]";
        }
        if (codes.Count(static code => code == EvaluationMode) > 1)
        {
            yield return $"{EvaluationMode} is warned more than once";
        }
    }

    private const string EvaluationMode = "EVALUATION_MODE";

    /// <summary>
    /// Resolves <c>a.b[0].c</c>, where <c>[name=value]</c> keeps, in their order, the items of an
    /// array whose <c>name</c> is the string <c>value</c>, such as
    /// <c>warnings[code=LOSSY_CONVERSION][0]</c>; true when every segment exists.
    /// </summary>
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
                if (value is not JsonArray array || !TrySelect(array, segment[(open + 1)..close], out value))
                {
                    value = null;
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>The item an index names in an array, or the array of the items a <c>name=value</c> filter keeps.</summary>
    private static bool TrySelect(JsonArray array, string selector, out JsonNode? item)
    {
        int equals = selector.IndexOf('=', StringComparison.Ordinal);
        if (equals < 0)
        {
            int index = int.Parse(selector, NumberStyles.None, CultureInfo.InvariantCulture);
            item = index < array.Count ? array[index] : null;
            return index < array.Count;
        }
        item = new JsonArray([.. array
            .Where(element => element is JsonObject obj && obj[selector[..equals]] is JsonValue key
                && key.TryGetValue(out string? text) && text == selector[(equals + 1)..])
            .Select(static element => element!.DeepClone())]);
        return true;
    }

    /// <summary>
    /// The files a step may change, by workspace-relative path: the workspace's and, when
    /// <paramref name="installation"/> is set, the user configuration's under
    /// <c>&lt;config&gt;/</c> and the CLI's installation directory's under <c>&lt;install&gt;/</c>,
    /// these by size and time since that directory is large and only an installer writes it.
    /// </summary>
    private static IReadOnlyDictionary<string, string> Snapshot(TempWorkspace workspace, bool installation)
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in Directory.EnumerateFiles(workspace.Path, "*", SearchOption.AllDirectories))
        {
            string relative = Normalize(Path.GetRelativePath(workspace.Path, path));
            if (!relative.Equals(ScenarioLicense.ProjectLicense, StringComparison.OrdinalIgnoreCase))
            {
                files[relative] = FileHashes.Sha256(path);
            }
        }
        if (installation)
        {
            string configuration = Path.GetDirectoryName(workspace.ConfigDirectory)!;
            foreach (string path in Directory.EnumerateFiles(configuration, "*", SearchOption.AllDirectories))
            {
                files[ConfigurationPrefix + Normalize(Path.GetRelativePath(configuration, path))] = FileHashes.Sha256(path);
            }
            string installed = Path.GetDirectoryName(CliRunner.ExecutablePath)!;
            foreach (FileInfo file in new DirectoryInfo(installed).EnumerateFiles("*", SearchOption.AllDirectories))
            {
                files[InstallationPrefix + Normalize(Path.GetRelativePath(installed, file.FullName))] =
                    string.Create(CultureInfo.InvariantCulture, $"{file.Length}:{file.LastWriteTimeUtc.Ticks}");
            }
        }
        return files;
    }

    /// <summary>
    /// Whether the files a step writes may start with a UTF-8 byte order mark
    /// (<see cref="ScenarioContract.ByteOrderMark"/>): the step asks for one with <c>--bom</c>,
    /// edits an input that starts with one, or extracts the files a document embeds, which keep
    /// the bytes the document stores.
    /// </summary>
    private static bool KeepsByteOrderMarks(TempWorkspace workspace, IReadOnlyList<string> args) =>
        args.Contains("--bom")
        || args.Count > 2 && args[1] == "edit" && File.Exists(workspace.File(args[2])) && StartsWithByteOrderMark(workspace.File(args[2]))
        || args.Count > 1 && args[1] == "extract" && EmbeddedFileKinds.Contains((args[0], What(args)));

    /// <summary>
    /// The <c>extract --what</c> kinds of each product that copy the files a document embeds as it
    /// stores them, the ones its extract handler names <c>ExtractionContent.EmbeddedFiles</c>.
    /// </summary>
    private static readonly HashSet<(string Product, string? What)> EmbeddedFileKinds =
        [("pdf", "attachments"), ("words", "images"), ("slides", "media")];

    /// <summary>The value of a step's <c>--what</c>, null without one.</summary>
    private static string? What(IReadOnlyList<string> args)
    {
        for (int index = 0; index < args.Count; index++)
        {
            if (args[index] == "--what" && index + 1 < args.Count) { return args[index + 1]; }
            if (args[index].StartsWith("--what=", StringComparison.Ordinal)) { return args[index]["--what=".Length..]; }
        }
        return null;
    }

    private static bool StartsWithByteOrderMark(string path)
    {
        Span<byte> head = stackalloc byte[3];
        using FileStream file = File.OpenRead(path);
        return file.ReadAtLeast(head, head.Length, throwOnEndOfStream: false) == head.Length && head.SequenceEqual(Encoding.UTF8.Preamble);
    }

    private const string ConfigurationPrefix = "<config>/";

    private const string InstallationPrefix = "<install>/";

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
