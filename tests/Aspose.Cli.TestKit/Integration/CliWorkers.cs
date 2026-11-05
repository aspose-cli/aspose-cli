using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Configuration;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.TestKit;

/// <summary>
/// Warm <c>aspose-cli</c> worker processes that run the command lines of <see cref="CliProcess"/>
/// for a test project that enables them, so a command no longer pays the start of a process and
/// its engines. Each worker is the tested executable itself, started with a startup hook
/// (<see cref="CliWorkerHost"/>), and runs one command at a time with the working directory,
/// environment and console streams a child process would get, so commands that run side by side
/// never share output. A worker that crashes or exceeds the timeout fails its command with what
/// the worker wrote, and is never replaced by a silent rerun. The one command that runs again is
/// one that fails with <c>EVALUATION_LIMIT</c> in a worker that ran other commands, a limit the
/// worker itself can reach (see <see cref="Run"/>).
/// </summary>
/// <remarks>
/// An engine's license is process-wide, so a worker serves one license lane: the commands whose
/// workspace holds the same project license, or none. The command lines whose behavior a warm
/// process could change run as child processes: any standard input, a license option, another
/// working directory, a font directory (engines keep the fonts they load process-wide), a user or
/// product license file, the license commands, and the commands that start services or a browser.
/// </remarks>
public static class CliWorkers
{
    /// <summary>
    /// The number of workers; <c>0</c> runs every command as a child process. Unset, a project that
    /// enables workers gets up to eight.
    /// </summary>
    public const string CountVariable = DistributionInfo.EnvironmentVariablePrefix + "TEST_WORKERS";

    /// <summary>The commands a worker serves before it is replaced, which bounds what a leak can grow to.</summary>
    private const int CommandsPerWorker = 250;

    private static readonly object Gate = new();
    private static readonly Dictionary<string, Stack<CliWorker>> Idle = new(StringComparer.Ordinal);
    private static readonly HashSet<CliWorker> Live = [];
    private static readonly AsyncLocal<bool> Suspended = new();
    private static SemaphoreSlim? _slots;
    private static int _capacity;

    /// <summary>Lets <see cref="CliProcess"/> run this test process's commands in workers, unless <see cref="CountVariable"/> is <c>0</c>.</summary>
    public static void Enable()
    {
        lock (Gate)
        {
            if (_slots is not null)
            {
                return;
            }
            string? configured = Environment.GetEnvironmentVariable(CountVariable);
            int capacity = string.IsNullOrWhiteSpace(configured)
                ? Math.Min(8, Environment.ProcessorCount)
                : int.Parse(configured, System.Globalization.CultureInfo.InvariantCulture);
            if (capacity <= 0)
            {
                return;
            }
            _capacity = capacity;
            _slots = new SemaphoreSlim(capacity, capacity);
            AppDomain.CurrentDomain.ProcessExit += static (_, _) => StopAll();
        }
    }

    /// <summary>Whether this test process runs commands in workers.</summary>
    public static bool Enabled => _slots is not null;

    /// <summary>The runs workers have made for the current thread, so a test can tell where its runs went.</summary>
    [ThreadStatic]
    private static int _threadRuns;

    /// <summary>
    /// The runs workers have made for the current thread: one for each command line, and one more
    /// for a command line that ran again in a new worker (see <see cref="Run"/>).
    /// </summary>
    public static int ThreadRuns => _threadRuns;

    /// <summary>Runs the commands of this flow as child processes until disposed.</summary>
    public static IDisposable ChildProcesses()
    {
        bool outer = Suspended.Value;
        Suspended.Value = true;
        return new Restore(() => Suspended.Value = outer);
    }

    /// <summary>
    /// The license lane a worker can run a command line in, or null when it runs as a child
    /// process (see the remarks of <see cref="CliWorkers"/>).
    /// </summary>
    internal static string? LaneOf(string executable, string directory, IReadOnlyList<string> args, string? standardInput, IDictionary<string, string?> environment)
    {
        if (!Enabled || Suspended.Value || !string.IsNullOrEmpty(standardInput)
            || !CliRunner.TryGetExecutablePath(out string? tested)
            || !string.Equals(Path.GetFullPath(executable), tested, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        if (args.Count > 0 && ChildCommands.Contains(args[0]))
        {
            return null;
        }
        if (args.Count > 1 && args[1] == "open")
        {
            return null;
        }
        foreach (string arg in args)
        {
            if (arg == "--")
            {
                break;
            }
            if (ChildOptions.Any(option => arg == option || arg.StartsWith(option + "=", StringComparison.Ordinal)))
            {
                return null;
            }
        }
        if (Directory.Exists(Path.Combine(directory, ".aspose", "licenses")))
        {
            return null;
        }
        if (environment.TryGetValue(ConfigurationPaths.EnvironmentVariableName, out string? configuration) && configuration is not null
            && (File.Exists(Path.Combine(configuration, "license.lic")) || Directory.Exists(Path.Combine(configuration, "licenses"))))
        {
            return null;
        }
        string project = Path.Combine(directory, ".aspose", "license.lic");
        return File.Exists(project)
            ? "licensed:" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(project)))
            : "evaluation";
    }

    /// <summary>The first words of the command lines that run as child processes: license state, services, the App, a browser, updates and MCP.</summary>
    private static readonly HashSet<string> ChildCommands = new(StringComparer.Ordinal)
    {
        "license", "app", "preview", "update", "mcp", "__render-worker", "__viewer-service",
    };

    /// <summary>
    /// The options whose command lines run as child processes: the global options that choose the
    /// license or the working directory, and a font directory, whose fonts an engine may keep for
    /// the rest of the process.
    /// </summary>
    private static readonly string[] ChildOptions = [GlobalOptionNames.License, GlobalOptionNames.LicenseMode, GlobalOptionNames.WorkDir, "--font-dir"];

    /// <summary>
    /// Runs a command line in a worker of <paramref name="lane"/>. A command that fails with
    /// <c>EVALUATION_LIMIT</c> in a worker that ran other commands runs again in a new worker,
    /// whose result is the command's.
    /// </summary>
    internal static CliResult Run(string lane, string executable, string directory, IReadOnlyList<string> args, IDictionary<string, string?> environment, TimeSpan timeout)
    {
        SemaphoreSlim slots = _slots ?? throw new InvalidOperationException("Workers are not enabled.");
        slots.Wait();
        try
        {
            CliWorker worker = Take(lane, executable, reuse: true);
            bool ranOthers = worker.Commands > 0;
            CliResult result = RunIn(worker, directory, args, environment, timeout);
            // An evaluation limit can be the worker's own doing: Aspose.Cells evaluation mode opens
            // at most 100 files in one process, which a worker reaches across commands while one
            // command line in a process of its own, the child process a worker stands in for,
            // never does. So this is no silent rerun of a crash: the worker answered, and the new
            // worker gives the command the fresh process a child would have. A limit the command
            // reaches by itself it reaches there too, and that is the result; the product's
            // preview render worker recycles itself by the same rule.
            if (ranOthers && IsEvaluationLimit(result))
            {
                Stop(worker);
                worker = Take(lane, executable, reuse: false);
                result = RunIn(worker, directory, args, environment, timeout);
            }
            Return(lane, worker);
            return result;
        }
        finally
        {
            slots.Release();
        }
    }

    /// <summary>
    /// Whether a command failed with <c>EVALUATION_LIMIT</c>. Its exit code, 7, is also that of
    /// the other license errors, <c>LICENSE_FILE_NOT_FOUND</c> and <c>LICENSE_INVALID</c>, so the
    /// code is read from the error the command wrote to stderr: in the JSON formats the
    /// <c>error.code</c> of the error envelope, after any JSON lines of diagnostics, and in the table
    /// and Markdown formats the line <c>error EVALUATION_LIMIT: ...</c>.
    /// </summary>
    private static bool IsEvaluationLimit(CliResult result)
    {
        if (result.ExitCode != (int)ExitCode.LicenseError)
        {
            return false;
        }
        string code = ErrorCodes.EvaluationLimit.Name;
        string line = $"error {code}: ";
        if (result.StdErr.Split('\n').Any(text => text.StartsWith(line, StringComparison.Ordinal)))
        {
            return true;
        }
        try
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(result.StdErr), new JsonReaderOptions { AllowMultipleValues = true });
            while (reader.Read())
            {
                using JsonDocument document = JsonDocument.ParseValue(ref reader);
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("error", out JsonElement error)
                    && error.ValueKind == JsonValueKind.Object
                    && error.TryGetProperty("code", out JsonElement value)
                    && value.ValueKind == JsonValueKind.String
                    && value.ValueEquals(code))
                {
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            // Not the stderr of a JSON format.
        }
        return false;
    }

    /// <summary>Runs a command line in <paramref name="worker"/>, which is stopped when it does not answer.</summary>
    private static CliResult RunIn(CliWorker worker, string directory, IReadOnlyList<string> args, IDictionary<string, string?> environment, TimeSpan timeout)
    {
        bool answered = false;
        try
        {
            CliResult result = worker.Run(directory, args, environment, timeout);
            answered = true;
            _threadRuns++;
            return result;
        }
        finally
        {
            if (!answered)
            {
                Stop(worker);
            }
        }
    }

    /// <summary>Makes a worker idle in its lane, or stops it once it has served its commands.</summary>
    private static void Return(string lane, CliWorker worker)
    {
        if (worker.Commands >= CommandsPerWorker)
        {
            Stop(worker);
            return;
        }
        lock (Gate)
        {
            if (!Idle.TryGetValue(lane, out Stack<CliWorker>? idle))
            {
                Idle[lane] = idle = new Stack<CliWorker>();
            }
            idle.Push(worker);
        }
    }

    /// <summary>
    /// An idle worker of the lane when <paramref name="reuse"/> allows one, or a new one; at
    /// capacity an idle worker makes room. Holding a slot guarantees one: fewer workers than the
    /// capacity are busy.
    /// </summary>
    private static CliWorker Take(string lane, string executable, bool reuse)
    {
        CliWorker? evicted = null;
        lock (Gate)
        {
            if (reuse && Idle.TryGetValue(lane, out Stack<CliWorker>? idle) && idle.Count > 0)
            {
                return idle.Pop();
            }
            if (Live.Count >= _capacity)
            {
                Stack<CliWorker> other = Idle.Values.First(static stack => stack.Count > 0);
                evicted = other.Pop();
                Live.Remove(evicted);
            }
        }
        evicted?.Dispose();
        CliWorker started = CliWorker.Start(executable, lane);
        lock (Gate)
        {
            Live.Add(started);
        }
        return started;
    }

    private static void Stop(CliWorker worker)
    {
        lock (Gate)
        {
            Live.Remove(worker);
        }
        worker.Dispose();
    }

    private static void StopAll()
    {
        CliWorker[] workers;
        lock (Gate)
        {
            workers = [.. Live];
            Live.Clear();
            Idle.Clear();
        }
        foreach (CliWorker worker in workers)
        {
            worker.Dispose();
        }
    }

    private sealed class Restore(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}

/// <summary>One warm <c>aspose-cli</c> process and the pipe it takes command lines from.</summary>
internal sealed class CliWorker : IDisposable
{
    private const int TailCharacters = 16 * 1024;

    private readonly Process _process;
    private readonly NamedPipeServerStream _pipe;
    private readonly BinaryReader _reader;
    private readonly BinaryWriter _writer;
    private readonly TempDirectory _home;
    private readonly StringBuilder _output = new();

    private CliWorker(Process process, NamedPipeServerStream pipe, TempDirectory home, string lane)
    {
        _process = process;
        _pipe = pipe;
        _home = home;
        Lane = lane;
        _reader = new BinaryReader(pipe, CliWorkerHost.Utf8, leaveOpen: true);
        _writer = new BinaryWriter(pipe, CliWorkerHost.Utf8, leaveOpen: true);
    }

    public string Lane { get; }

    /// <summary>The commands this worker has run.</summary>
    public int Commands { get; private set; }

    /// <summary>
    /// Starts a worker of <paramref name="executable"/> for <paramref name="lane"/> with the test
    /// process's environment and <paramref name="variables"/>.
    /// </summary>
    public static CliWorker Start(string executable, string lane, IReadOnlyDictionary<string, string?>? variables = null)
    {
        string name = "aspose-cli-test-worker-" + Guid.NewGuid().ToString("N");
        var pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var home = new TempDirectory();
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = home.Path,
            UseShellExecute = false,
            CreateNoWindow = true,
            ErrorDialog = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach ((string key, string? value) in variables ?? new Dictionary<string, string?>())
        {
            start.Environment[key] = value;
        }
        // The run's own hooks, such as the culture hook of scripts/test.ps1 -CiLike, run first;
        // the worker's hook never returns, and gives the variable back its value.
        string? hooks = start.Environment.TryGetValue(CliWorkerHost.HookVariable, out string? before) ? before : null;
        start.Environment[CliWorkerHost.HooksBeforeVariable] = hooks;
        start.Environment[CliWorkerHost.HookVariable] = string.IsNullOrEmpty(hooks)
            ? typeof(CliWorkerHost).Assembly.Location
            : hooks + Path.PathSeparator + typeof(CliWorkerHost).Assembly.Location;
        start.Environment[CliWorkerHost.PipeVariable] = name;
        Process process = Process.Start(start) ?? throw new InvalidOperationException($"Failed to start {executable}");
        var worker = new CliWorker(process, pipe, home, lane);
        process.OutputDataReceived += (_, line) => worker.Record("stdout", line.Data);
        process.ErrorDataReceived += (_, line) => worker.Record("stderr", line.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.StandardInput.Close();
        Task connected = pipe.WaitForConnectionAsync();
        if (Task.WaitAny([connected, process.WaitForExitAsync()], TimeSpan.FromSeconds(60)) != 0 || !connected.IsCompletedSuccessfully)
        {
            string failure = worker.Failure($"did not connect (exited: {process.HasExited})");
            worker.Dispose();
            throw new InvalidOperationException(failure);
        }
        return worker;
    }

    public CliResult Run(string directory, IReadOnlyList<string> args, IDictionary<string, string?> environment, TimeSpan timeout)
    {
        Commands++;
        string command = $"aspose-cli {string.Join(' ', args)}";
        try
        {
            _writer.Write(CliWorkerHost.Magic);
            _writer.Write(directory);
            _writer.Write(args.Count);
            foreach (string arg in args)
            {
                _writer.Write(arg);
            }
            KeyValuePair<string, string?>[] variables = [.. environment.Where(static pair => pair.Value is not null)];
            _writer.Write(variables.Length);
            foreach ((string key, string? value) in variables)
            {
                _writer.Write(key);
                _writer.Write(value!);
            }
            _writer.Flush();
        }
        catch (IOException exception)
        {
            throw new InvalidOperationException(Failure($"could not take {command}"), exception);
        }

        Task<CliResult> response = Task.Run(ReadResponse);
        bool finished;
        try
        {
            finished = response.Wait(timeout);
        }
        catch (AggregateException exception) when (exception.InnerException is EndOfStreamException or IOException or InvalidDataException)
        {
            _process.WaitForExit(10_000);
            throw new InvalidOperationException(Failure($"crashed while running {command}"), exception.InnerException);
        }
        if (!finished)
        {
            Kill();
            throw new TimeoutException(Failure($"did not finish {command} within {timeout.TotalSeconds:N0}s"));
        }
        return response.Result;
    }

    private CliResult ReadResponse()
    {
        if (_reader.ReadInt32() != CliWorkerHost.Magic)
        {
            throw new InvalidDataException("The worker answered out of protocol.");
        }
        int exitCode = _reader.ReadInt32();
        string stdout = Decode(_reader.ReadBytes(_reader.ReadInt32()));
        string stderr = Decode(_reader.ReadBytes(_reader.ReadInt32()));
        return new CliResult(exitCode, stdout, stderr);
    }

    /// <summary>Decodes a console stream the way <see cref="CliProcess"/> reads a child's.</summary>
    private static string Decode(byte[] bytes)
    {
        using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private void Record(string stream, string? line)
    {
        if (line is null)
        {
            return;
        }
        lock (_output)
        {
            _output.Append(stream).Append(": ").AppendLine(line);
            if (_output.Length > TailCharacters)
            {
                _output.Remove(0, _output.Length - TailCharacters);
            }
        }
    }

    private string Failure(string what)
    {
        string exit = _process.HasExited ? $" (exit {_process.ExitCode})" : string.Empty;
        lock (_output)
        {
            return $"The aspose-cli worker {_process.Id} of lane {Lane} {what}{exit}, after {Commands} commands. What the worker wrote outside its commands:"
                + Environment.NewLine + (_output.Length == 0 ? "(nothing)" : _output.ToString());
        }
    }

    private void Kill()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(10_000);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // It exited meanwhile.
        }
    }

    public void Dispose()
    {
        try
        {
            _pipe.Dispose(); // An idle worker ends when its pipe closes.
            if (!_process.WaitForExit(5_000))
            {
                Kill();
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // It exited meanwhile.
        }
        finally
        {
            _process.Dispose();
            _home.Dispose();
        }
    }
}
