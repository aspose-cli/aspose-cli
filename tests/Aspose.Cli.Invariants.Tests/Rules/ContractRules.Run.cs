using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>One JSON run of a rule: its exit code and the document it wrote, the result or the error envelope.</summary>
internal sealed record RuleRun(int ExitCode, JsonNode? Document, string Failure)
{
    public string? Error => Document?["error"]?["code"]?.GetValue<string>();

    /// <summary>
    /// Whether the run hit a limit of evaluation mode, such as the pages a PDF engine reads:
    /// what the rule checks cannot be seen in this license mode, so the case does not judge it.
    /// </summary>
    public bool Unjudged => Error == "EVALUATION_LIMIT";

    /// <summary>The failure in one line: the exit code, the error code and the message.</summary>
    public string Describe() => $"exit {ExitCode}{(Error is null ? string.Empty : " " + Error)}: {Failure}";
}

/// <summary>
/// The one way the contract rules run the CLI outside a scenario: a fresh workspace under the
/// run's license mode, with its own isolated configuration, in which a rule runs several command
/// lines and judges what a scenario expectation cannot state, such as stdout and stderr text, the
/// difference between two runs or a value written and read back. A workspace made from an
/// <see cref="Invocation"/> holds its files and runs with its environment; any other runs with
/// <see cref="SecretVariable"/> holding the fixture password.
/// </summary>
internal sealed partial class RuleWorkspace : IDisposable
{
    /// <summary>The variable that holds the fixture password for secret fields and encrypted documents.</summary>
    public const string SecretVariable = "INVARIANT_SECRET";

    private readonly TempWorkspace _workspace = new();
    private readonly Invocation? _invocation;
    private readonly Dictionary<string, string?> _environment;

    public RuleWorkspace()
        : this(null)
    {
    }

    public RuleWorkspace(Invocation? invocation)
    {
        _invocation = invocation;
        ScenarioLicense.Project(_workspace.Path);
        _environment = invocation is null
            ? new(StringComparer.Ordinal) { [SecretVariable] = ScenarioFixtures.Password }
            : invocation.Env.ToDictionary(static pair => pair.Key, static pair => (string?)pair.Value, StringComparer.Ordinal);
        foreach ((string relative, ScenarioFile file) in invocation?.Files ?? [])
        {
            Seed(relative, file switch
            {
                { Fixture: { } fixture } => ScenarioFixtures.Read(fixture),
                { Text: { } text } => Encoding.UTF8.GetBytes(text),
                { Base64: { } base64 } => Convert.FromBase64String(base64),
                _ => throw new InvalidOperationException($"Seed file '{relative}' has no fixture, text or Base64 source."),
            });
        }
    }

    public string Root => _workspace.Path;

    /// <summary>The isolated configuration directory, where the host commands keep their state.</summary>
    public string ConfigRoot => Path.GetDirectoryName(_workspace.ConfigDirectory)!;

    public string File(string relative) => _workspace.File(relative);

    public void Seed(string relative, byte[] bytes)
    {
        string target = File(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        System.IO.File.WriteAllBytes(target, bytes);
    }

    public void Seed(string relative, string text) => Seed(relative, Encoding.UTF8.GetBytes(text));

    /// <summary>
    /// The hint problems of the runs of a rule case's check (<see cref="CollectHintProblems"/>);
    /// null outside one.
    /// </summary>
    private static readonly AsyncLocal<List<string>?> CaseHintProblems = new();

    /// <summary>A test's replacement for what the CLI wrote, in the flow that set it; null to keep the CLI's.</summary>
    private static readonly AsyncLocal<Func<CliResult, CliResult>?> ResultRewrite = new();

    /// <summary>
    /// Replaces what each run writes in this flow until disposed, so a test can give a rule run an
    /// envelope the CLI does not write, such as one whose hint does not run.
    /// </summary>
    internal static IDisposable RewriteResults(Func<CliResult, CliResult> rewrite)
    {
        Func<CliResult, CliResult>? outer = ResultRewrite.Value;
        ResultRewrite.Value = rewrite;
        return new Restore(() => ResultRewrite.Value = outer);
    }

    private sealed class Restore(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }

    /// <summary>
    /// Runs a rule case's check and appends, as problems of its invariant prefixed
    /// <c>executable-hint:</c>, every hint of its JSON runs that does not run as written, so
    /// known violations cover them by case.
    /// </summary>
    public static IReadOnlyList<ScenarioProblem> CollectHintProblems(string invariant, Func<IReadOnlyList<ScenarioProblem>> check)
    {
        List<string>? outer = CaseHintProblems.Value;
        var collected = new List<string>();
        CaseHintProblems.Value = collected;
        try
        {
            IReadOnlyList<ScenarioProblem> problems = check();
            lock (collected)
            {
                return
                [
                    .. problems,
                    .. collected.Distinct(StringComparer.Ordinal)
                        .Select(problem => new ScenarioProblem(0, invariant, ExecutableHints.Check + ": " + problem)),
                ];
            }
        }
        finally
        {
            CaseHintProblems.Value = outer;
        }
    }

    /// <summary>Runs a command line as it is, with empty stdin; a JSON run's hints must run as written.</summary>
    public CliResult Execute(params string[] args)
    {
        CliResult result = new CliProcess(CliRunner.ExecutablePath, Environment()).Run(Root, string.Empty, args);
        if (ResultRewrite.Value is { } rewrite)
        {
            result = rewrite(result);
        }
        IReadOnlyList<string> hintProblems = RequestsJson(args)
            ? [.. ExecutableHints.Problems(new HintRun(args, Root, ConfigRoot, _environment), result).Select(Stable)]
            : [];
        if (hintProblems.Count > 0 && CaseHintProblems.Value is { } collected)
        {
            lock (collected)
            {
                collected.AddRange(hintProblems);
            }
        }
        return result;
    }

    private static bool RequestsJson(IReadOnlyList<string> args) =>
        args.Select((arg, index) => arg == "--output-format=json" || (arg == "--output-format" && index + 1 < args.Count && args[index + 1] == "json"))
            .Any(static requested => requested);

    /// <summary>Runs the invocation's arguments followed by <paramref name="extra"/>, with empty stdin.</summary>
    public CliResult Run(params string[] extra) =>
        Execute([.. (_invocation ?? throw new InvalidOperationException("The workspace has no invocation.")).Args, .. extra]);

    /// <summary>Runs a command line with JSON output and reads the document it wrote.</summary>
    public RuleRun Json(params string[] args)
    {
        CliResult result = Execute([.. args, "--output-format", "json"]);
        JsonNode? document = Parse(result.StdOut) ?? Parse(result.StdErr);
        string failure = document?["error"]?["message"]?.GetValue<string>() ?? result.StdErr;
        return new RuleRun(result.ExitCode, document, Stable(ScenarioContract.Excerpt(failure)));
    }

    /// <summary>
    /// Runs the invocation's arguments and presses Ctrl+C after <paramref name="delay"/>, as a
    /// user at a terminal does: a Windows PowerShell helper, started with a console of its own and
    /// no window, starts the CLI on that console, then raises <c>CTRL_C_EVENT</c> for every
    /// process on it while it ignores the event itself. Returns the CLI's exit code and the
    /// workspace entries the run left beside the seed files.
    /// </summary>
    public (int ExitCode, string[] Left) Interrupt(TimeSpan delay)
    {
        Invocation invocation = _invocation ?? throw new InvalidOperationException("The workspace has no invocation.");
        if (invocation.Args.FirstOrDefault(static arg => arg.Length == 0 || arg.Any(static character => character is ' ' or '"')) is { } unsafeArg)
        {
            throw new InvalidOperationException($"The interrupt helper passes arguments without quoting: '{unsafeArg}'.");
        }
        string script = Path.Combine(ConfigRoot, "interrupt.ps1");
        System.IO.File.WriteAllText(script, InterruptScript);
        var start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        string[] args =
        [
            "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script,
            "-Exe", CliRunner.ExecutablePath,
            "-DelayMs", ((int)delay.TotalMilliseconds).ToString(CultureInfo.InvariantCulture),
            "-CliArgs", string.Join(' ', invocation.Args),
        ];
        foreach (string arg in args)
        {
            start.ArgumentList.Add(arg);
        }
        Environment().Apply(start.Environment);
        using Process helper = Process.Start(start) ?? throw new InvalidOperationException("The interrupt helper did not start.");
        Task<string> output = helper.StandardOutput.ReadToEndAsync();
        Task<string> error = helper.StandardError.ReadToEndAsync();
        if (!helper.WaitForExit(TimeSpan.FromMinutes(2)))
        {
            helper.Kill(entireProcessTree: true);
            throw new TimeoutException("The interrupted CLI did not exit within two minutes.");
        }
        string exit = output.Result.ReplaceLineEndings("\n").Split('\n').LastOrDefault(static line => line.StartsWith("exit=", StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"The interrupt helper reported no exit code: {error.Result}");
        string[] left =
        [
            .. Directory.EnumerateFileSystemEntries(Root, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(Root, path).Replace('\\', '/'))
                .Where(path => !path.StartsWith(ScenarioLicense.ProjectDirectory, StringComparison.OrdinalIgnoreCase) && !invocation.Files.ContainsKey(path))
                .Order(StringComparer.Ordinal),
        ];
        return (int.Parse(exit["exit=".Length..], CultureInfo.InvariantCulture), left);
    }

    /// <summary>Content hashes by relative path of the workspace and the configuration directory, without the project license.</summary>
    public IReadOnlyDictionary<string, string> Snapshot()
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string root, string prefix) in new[] { (Root, string.Empty), (ConfigRoot, "<config>/") })
        {
            foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                string relative = prefix + Path.GetRelativePath(root, path).Replace('\\', '/');
                if (!relative.StartsWith(ScenarioLicense.ProjectDirectory + "/", StringComparison.OrdinalIgnoreCase))
                {
                    files[relative] = FileHashes.Sha256(path);
                }
            }
        }
        return files;
    }

    /// <summary>The paths a run created, changed or deleted, sorted.</summary>
    public static string[] Written(IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after) =>
    [
        .. after.Where(pair => !before.TryGetValue(pair.Key, out string? hash) || hash != pair.Value).Select(static pair => pair.Key)
            .Concat(before.Keys.Where(key => !after.ContainsKey(key)))
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>A message with the workspace's random path replaced, so it reads the same in every run.</summary>
    public string Clean(string message) =>
        message
            .Replace(Root.Replace("\\", "\\\\", StringComparison.Ordinal), "<workspace>", StringComparison.OrdinalIgnoreCase)
            .Replace(Root, "<workspace>", StringComparison.OrdinalIgnoreCase);

    /// <summary>A message that names the workspace, not its random path, and no generated id.</summary>
    public string Stable(string message) => GeneratedId().Replace(Clean(message), "<id>");

    public void Dispose() => _workspace.Dispose();

    private CliEnvironment Environment() => CliEnvironment.Evaluation(ConfigRoot, _environment);

    private static JsonNode? Parse(string text)
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

    // CREATE_NO_WINDOW starts the helper in a process group that ignores Ctrl+C, which its
    // children inherit, so it restores the default before it starts the CLI.
    private const string InterruptScript = """
        param([string]$Exe, [int]$DelayMs, [string]$CliArgs)
        Add-Type -Namespace Invariants -Name Console -MemberDefinition @'
        [DllImport("kernel32.dll")] public static extern bool GenerateConsoleCtrlEvent(uint ctrlEvent, uint processGroupId);
        [DllImport("kernel32.dll")] public static extern bool SetConsoleCtrlHandler(System.IntPtr handler, bool add);
        '@
        [void][Invariants.Console]::SetConsoleCtrlHandler([System.IntPtr]::Zero, $false)
        $start = New-Object System.Diagnostics.ProcessStartInfo $Exe
        $start.Arguments = $CliArgs
        $start.UseShellExecute = $false
        $cli = [System.Diagnostics.Process]::Start($start)
        Start-Sleep -Milliseconds $DelayMs
        [void][Invariants.Console]::SetConsoleCtrlHandler([System.IntPtr]::Zero, $true)
        [void][Invariants.Console]::GenerateConsoleCtrlEvent(0, 0)
        $cli.WaitForExit()
        "exit=$($cli.ExitCode)"
        """;

    [GeneratedRegex("(?=[0-9a-f-]*[0-9])[0-9a-f][0-9a-f-]{14,}[0-9a-f]")]
    private static partial Regex GeneratedId();
}
