using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// A workspace seeded with an <see cref="Invocation"/>'s files, under the run's license mode,
/// in which a rule runs the invocation's command line several times and judges its streams
/// itself: what a scenario expectation cannot state, such as stdout and stderr text or the
/// difference between two runs.
/// </summary>
internal sealed class InvocationSession : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly Invocation _invocation;

    public InvocationSession(Invocation invocation)
    {
        _invocation = invocation;
        ScenarioLicense.Project(_workspace.Path);
        foreach ((string relative, ScenarioFile file) in invocation.Files)
        {
            byte[] bytes = file switch
            {
                { Fixture: { } fixture } => ScenarioFixtures.Read(fixture),
                { Text: { } text } => Encoding.UTF8.GetBytes(text),
                { Base64: { } base64 } => Convert.FromBase64String(base64),
                _ => throw new InvalidOperationException($"Seed file '{relative}' has no fixture, text or Base64 source."),
            };
            File.WriteAllBytes(_workspace.File(relative), bytes);
        }
    }

    /// <summary>Runs the invocation's arguments followed by <paramref name="extra"/>, with empty stdin.</summary>
    public CliResult Run(params string[] extra) =>
        new CliProcess(CliRunner.ExecutablePath, Environment()).Run(_workspace.Path, string.Empty, [.. _invocation.Args, .. extra]);

    /// <summary>
    /// Runs the invocation's arguments and presses Ctrl+C after <paramref name="delay"/>, as a
    /// user at a terminal does: a Windows PowerShell helper, started with a console of its own and
    /// no window, starts the CLI on that console, then raises <c>CTRL_C_EVENT</c> for every
    /// process on it while it ignores the event itself. Returns the CLI's exit code and the
    /// workspace entries the run left beside the seed files.
    /// </summary>
    public (int ExitCode, string[] Left) Interrupt(TimeSpan delay)
    {
        if (_invocation.Args.FirstOrDefault(static arg => arg.Length == 0 || arg.Any(static character => character is ' ' or '"')) is { } unsafeArg)
        {
            throw new InvalidOperationException($"The interrupt helper passes arguments without quoting: '{unsafeArg}'.");
        }
        string script = Path.Combine(Path.GetDirectoryName(_workspace.ConfigDirectory)!, "interrupt.ps1");
        File.WriteAllText(script, InterruptScript);
        var start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _workspace.Path,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        string[] args =
        [
            "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script,
            "-Exe", CliRunner.ExecutablePath,
            "-DelayMs", ((int)delay.TotalMilliseconds).ToString(CultureInfo.InvariantCulture),
            "-CliArgs", string.Join(' ', _invocation.Args),
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
            .. Directory.EnumerateFileSystemEntries(_workspace.Path, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(_workspace.Path, path).Replace('\\', '/'))
                .Where(path => !path.StartsWith(ScenarioLicense.ProjectDirectory, StringComparison.OrdinalIgnoreCase) && !_invocation.Files.ContainsKey(path))
                .Order(StringComparer.Ordinal),
        ];
        return (int.Parse(exit["exit=".Length..], CultureInfo.InvariantCulture), left);
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

    private CliEnvironment Environment() =>
        CliEnvironment.Evaluation(
            Path.GetDirectoryName(_workspace.ConfigDirectory)!,
            _invocation.Env.ToDictionary(static pair => pair.Key, static pair => (string?)pair.Value));

    /// <summary>A message with the workspace's random path replaced, so it reads the same in every run.</summary>
    public string Clean(string message) =>
        message
            .Replace(_workspace.Path.Replace("\\", "\\\\", StringComparison.Ordinal), "<workspace>", StringComparison.OrdinalIgnoreCase)
            .Replace(_workspace.Path, "<workspace>", StringComparison.OrdinalIgnoreCase);

    public void Dispose() => _workspace.Dispose();
}

/// <summary>Helpers of the contract rules that run command lines (<c>InvocationRules.cs</c>).</summary>
internal static class InvocationChecks
{
    /// <summary>
    /// A case that runs its command lines in an <see cref="InvocationSession"/>: each message
    /// <paramref name="check"/> returns is one violation of <paramref name="rule"/>.
    /// </summary>
    public static InvariantCase Runs(
        string rule, string subject, string shard, bool slow, Invocation invocation, Func<InvocationSession, IEnumerable<string>> check) =>
        new($"{rule} | {subject}", rule, shard, slow, Scenario: null, Check: () =>
        {
            using var session = new InvocationSession(invocation);
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
