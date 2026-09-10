using System.Buffers;
using System.Diagnostics;
using System.Text;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.LocalServices;

namespace Aspose.Cli.Host.Mcp;

internal sealed class McpCommandRunner
{
    internal const int DefaultTimeoutSeconds = 120;
    internal const int MaximumArgumentCount = 256;
    internal const int MaximumArgumentBytes = 4 * 1024;
    internal const int MaximumArgumentsBytes = 64 * 1024;
    internal const int MaximumInputBytes = 1024 * 1024;
    internal const int MaximumOutputBytes = 4 * 1024 * 1024;
    internal static readonly TimeSpan ShutdownGracePeriod = TimeSpan.FromSeconds(2);
    private static readonly string[] BaselineEnvironmentVariables =
    [
        "SystemRoot", "WINDIR", "TEMP", "TMP", "PATH", "PATHEXT",
        "USERPROFILE", "HOMEDRIVE", "HOMEPATH", "HOME",
        "LOCALAPPDATA", "APPDATA", "PROGRAMDATA", "LANG", "LC_ALL", "LC_CTYPE",
    ];
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HashSet<string> _productRoots;
    private readonly Func<ProcessStartInfo> _startInfoFactory;

    public McpCommandRunner(IReadOnlyCollection<string> productRoots)
        : this(productRoots, CreateStartInfo)
    {
    }

    internal McpCommandRunner(
        IReadOnlyCollection<string> productRoots,
        Func<ProcessStartInfo> startInfoFactory)
    {
        ArgumentNullException.ThrowIfNull(productRoots);
        ArgumentNullException.ThrowIfNull(startInfoFactory);
        _productRoots = new HashSet<string>(productRoots, StringComparer.Ordinal);
        _startInfoFactory = startInfoFactory;
    }

    public Task<McpExecutionResult> RunCapabilitiesAsync(
        CancellationToken cancellationToken) =>
        RunCoreAsync(
            ["capabilities", "--output", "json"],
            stdin: null,
            DefaultTimeoutSeconds,
            cancellationToken);

    public Task<McpExecutionResult> RunAsync(
        string[] args,
        string? stdin,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        Validate(args, stdin, timeoutSeconds);
        EnsureAllowed(args);
        return RunCoreAsync(args, stdin, timeoutSeconds, cancellationToken);
    }

    internal void Validate(
        IReadOnlyList<string> args,
        string? stdin,
        int timeoutSeconds)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Count is 0 or > MaximumArgumentCount)
        {
            throw new McpCommandException(
                $"args must contain between 1 and {MaximumArgumentCount} values.");
        }

        int totalBytes = 0;
        foreach (string? argument in args)
        {
            if (argument is null)
            {
                throw new McpCommandException("args cannot contain null values.");
            }

            int bytes = Encoding.UTF8.GetByteCount(argument);
            if (bytes > MaximumArgumentBytes)
            {
                throw new McpCommandException(
                    $"Each argument must be at most {MaximumArgumentBytes} UTF-8 bytes.");
            }

            totalBytes = checked(totalBytes + bytes);
        }

        if (totalBytes > MaximumArgumentsBytes)
        {
            throw new McpCommandException(
                $"args must total at most {MaximumArgumentsBytes} UTF-8 bytes.");
        }

        if (stdin is not null
            && Encoding.UTF8.GetByteCount(stdin) > MaximumInputBytes)
        {
            throw new McpCommandException(
                $"stdin must be at most {MaximumInputBytes} UTF-8 bytes.");
        }

        if (timeoutSeconds is < 1 or > 600)
        {
            throw new McpCommandException("timeoutSeconds must be between 1 and 600.");
        }
    }

    internal void EnsureAllowed(IReadOnlyList<string> args)
    {
        IReadOnlyList<string> path = TimeoutWorkerSupervisor.ResolveCommandPath(args);
        bool allowed = path.Count > 0 && (
            _productRoots.Contains(path[0])
            || path[0] is "schema" or "doctor" or "docs"
            || path.Count == 2 && path[0] == "fonts" && path[1] is "list" or "check"
            || path.Count == 2 && path[0] == "preview" && path[1] == "status"
            || path.Count == 2 && path[0] == "app" && path[1] == "status");
        if (!allowed)
        {
            throw new McpCommandException(
                "The requested command is not available through MCP. " +
                "Use a compiled product command or an allowlisted read-only host command.");
        }
    }

    private async Task<McpExecutionResult> RunCoreAsync(
        string[] args,
        string? stdin,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ProcessStartInfo start = _startInfoFactory();
            start.RedirectStandardInput = true;
            start.StandardInputEncoding = new UTF8Encoding(false);
            start.StandardOutputEncoding = Encoding.UTF8;
            start.StandardErrorEncoding = Encoding.UTF8;
            start.WorkingDirectory = Directory.GetCurrentDirectory();
            ConfigureEnvironment(start, args);
            foreach (string argument in args)
            {
                start.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(start)
                ?? throw new McpCommandException("The CLI child process could not be started.");
            using IDisposable? job = WindowsProcessJob.TryAttach(process);
            using var timeout = new CancellationTokenSource(
                TimeSpan.FromSeconds(timeoutSeconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeout.Token);
            Task input = WriteInputAsync(process, stdin, linked.Token);
            Task<string> stdout = ReadBoundedAsync(
                process.StandardOutput.BaseStream,
                "stdout",
                process,
                linked.Token);
            Task<string> stderr = ReadBoundedAsync(
                process.StandardError.BaseStream,
                "stderr",
                process,
                linked.Token);
            try
            {
                await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
                await IgnoreClosedInputAsync(input, process).ConfigureAwait(false);
                string[] output = await Task.WhenAll(stdout, stderr)
                    .ConfigureAwait(false);
                return new McpExecutionResult
                {
                    ExitCode = process.ExitCode,
                    Stdout = output[0],
                    Stderr = output[1],
                };
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                TryKill(process);
                await ObserveShutdownAsync(
                    process,
                    job,
                    input,
                    stdout,
                    stderr).ConfigureAwait(false);
                throw new McpCommandException(
                    $"The CLI command exceeded the {timeoutSeconds}-second timeout.");
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                await ObserveShutdownAsync(
                    process,
                    job,
                    input,
                    stdout,
                    stderr).ConfigureAwait(false);
                throw;
            }
            finally
            {
                if (!process.HasExited)
                {
                    TryKill(process);
                }
            }
        }
        catch (McpCommandException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new McpCommandException("The CLI child process failed unexpectedly.");
        }
        finally
        {
            _gate.Release();
        }
    }

    private static ProcessStartInfo CreateStartInfo() =>
        SelfProcessLauncher.CreateBackground(
            "mcp",
            "Run MCP from the installed aspose-cli executable.");

    private static void ConfigureEnvironment(
        ProcessStartInfo start,
        IReadOnlyList<string> args)
    {
        var forwarded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in BaselineEnvironmentVariables.Concat(
                     ReferencedEnvironmentVariables(args)))
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (value is not null)
            {
                forwarded[name] = value;
            }
        }

        start.Environment.Clear();
        foreach ((string name, string value) in forwarded)
        {
            start.Environment[name] = value;
        }
    }

    internal static IReadOnlyList<string> ReferencedEnvironmentVariables(
        IReadOnlyList<string> args)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < args.Count; index++)
        {
            string argument = args[index];
            string? name = null;
            int equals = argument.IndexOf('=');
            if (equals > 0
                && argument[..equals].EndsWith("-env", StringComparison.Ordinal))
            {
                name = argument[(equals + 1)..];
            }
            else if (argument.EndsWith("-env", StringComparison.Ordinal)
                && index + 1 < args.Count)
            {
                name = args[++index];
            }

            if (name is not null && IsSafeEnvironmentName(name))
            {
                names.Add(name);
            }
        }

        return names.Order(StringComparer.Ordinal).ToArray();
    }

    private static bool IsSafeEnvironmentName(string name)
    {
        if (name.Length is 0 or > 128
            || !(char.IsAsciiLetter(name[0]) || name[0] == '_'))
        {
            return false;
        }

        foreach (char character in name.AsSpan(1))
        {
            if (!(char.IsAsciiLetterOrDigit(character) || character == '_'))
            {
                return false;
            }
        }

        return true;
    }

    private static async Task WriteInputAsync(
        Process process,
        string? input,
        CancellationToken cancellationToken)
    {
        try
        {
            if (input is not null)
            {
                await process.StandardInput.WriteAsync(
                    input.AsMemory(),
                    cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            process.StandardInput.Close();
        }
    }

    private static async Task<string> ReadBoundedAsync(
        Stream stream,
        string name,
        Process process,
        CancellationToken cancellationToken)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            using var output = new MemoryStream();
            while (true)
            {
                int read = await stream.ReadAsync(
                    buffer.AsMemory(),
                    cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return Encoding.UTF8.GetString(output.GetBuffer(), 0, (int)output.Length);
                }

                if (output.Length + read > MaximumOutputBytes)
                {
                    TryKill(process);
                    throw new McpCommandException(
                        $"The CLI {name} exceeded the {MaximumOutputBytes}-byte limit.");
                }

                output.Write(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task IgnoreClosedInputAsync(Task input, Process process)
    {
        try
        {
            await input.ConfigureAwait(false);
        }
        catch (IOException) when (process.HasExited)
        {
        }
        catch (ObjectDisposedException) when (process.HasExited)
        {
        }
    }

    private static async Task ObserveShutdownAsync(
        Process process,
        IDisposable? job,
        params Task[] tasks)
    {
        job?.Dispose();
        CloseRedirectedPipes(process);

        Task[] shutdownTasks =
        [
            IgnoreShutdownFailureAsync(process.WaitForExitAsync()),
            .. tasks.Select(IgnoreShutdownFailureAsync),
        ];
        try
        {
            await Task.WhenAll(shutdownTasks)
                .WaitAsync(ShutdownGracePeriod)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            TryKill(process);
            CloseRedirectedPipes(process);
        }
    }

    private static async Task IgnoreShutdownFailureAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception)
        {
        }
    }

    private static void CloseRedirectedPipes(Process process)
    {
        ClosePipe(process.StandardInput.BaseStream);
        ClosePipe(process.StandardOutput.BaseStream);
        ClosePipe(process.StandardError.BaseStream);
    }

    private static void ClosePipe(Stream pipe)
    {
        try
        {
            pipe.Dispose();
        }
        catch (Exception exception) when (
            exception is IOException
                or ObjectDisposedException)
        {
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
                or System.ComponentModel.Win32Exception
                or NotSupportedException)
        {
        }
    }
}

internal sealed class McpCommandException(string message) : Exception(message);
