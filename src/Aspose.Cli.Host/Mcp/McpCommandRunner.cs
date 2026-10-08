using System.Diagnostics;
using System.Text;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Host.Mcp;

internal sealed class McpCommandRunner
{
    internal const int DefaultTimeoutSeconds = 120;
    internal const int MaximumArgumentCount = 256;
    internal const int MaximumArgumentBytes = 4 * 1024;
    internal const int MaximumArgumentsBytes = 64 * 1024;
    internal const int MaximumInputBytes = 1024 * 1024;
    internal static readonly TimeSpan ShutdownGracePeriod = ChildProcess.TerminationGrace;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly InvocationParser _parser;
    private readonly GlobalValues? _inherited;
    private readonly string _workingDirectory;
    private readonly HostContext _host;
    private readonly Func<ProcessStartInfo> _startInfoFactory;

    public McpCommandRunner(HostContext host, GlobalValues? inherited = null)
        : this(host, CreateStartInfo, inherited)
    {
    }

    internal McpCommandRunner(
        HostContext host, Func<ProcessStartInfo> startInfoFactory,
        GlobalValues? inherited = null, InvocationParser? parser = null)
    {
        _host = host;
        _parser = parser ?? host.Parser;
        _startInfoFactory = startInfoFactory ?? throw new ArgumentNullException(nameof(startInfoFactory));
        _workingDirectory = Path.GetFullPath(inherited?.WorkDir ?? Directory.GetCurrentDirectory());
        _inherited = inherited is null ? null : inherited with
        {
            WorkDir = _workingDirectory,
            LicensePath = inherited.LicensePath is null ? null : Path.GetFullPath(inherited.LicensePath, _workingDirectory),
        };
    }

    public Task<McpExecutionResult> RunCapabilitiesAsync(
        CancellationToken cancellationToken) =>
        RunCoreAsync(
            ["capabilities", "--output", "json"],
            _parser.Parse(["capabilities", "--output", "json"], _inherited),
            stdin: null,
            DefaultTimeoutSeconds,
            cancellationToken);

    public Task<McpExecutionResult> RunAsync(
        string[] args,
        string? stdin,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        args = args.ToArray();
        Validate(args, stdin, timeoutSeconds);
        ParsedInvocation invocation = EnsureAllowed(args);
        return RunCoreAsync(args, invocation, stdin, timeoutSeconds, cancellationToken);
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

    internal ParsedInvocation EnsureAllowed(IReadOnlyList<string> args)
    {
        ParsedInvocation invocation;
        try { invocation = _parser.Parse(args.ToArray(), _inherited); }
        catch (Aspose.Cli.Sdk.Errors.CliException exception)
        {
            throw new McpCommandException(exception.Message);
        }
        if (invocation.ParseResult.Errors.Count > 0 || !invocation.McpAllowed)
        {
            throw new McpCommandException(
                "The requested command or arguments are not available through MCP. "
                + $"Use a product command or one of these host commands: {string.Join(", ", _parser.McpHostCommands())}.");
        }
        return invocation;
    }

    private async Task<McpExecutionResult> RunCoreAsync(
        string[] args,
        ParsedInvocation invocation,
        string? stdin,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            int effectiveSeconds = Math.Min(timeoutSeconds, invocation.GlobalValues?.TimeoutSeconds ?? timeoutSeconds);
            InvocationProcessResult result;
            try
            {
                result = await TimeoutWorkerSupervisor.ExecuteAsync(_host, args, invocation,
                    TimeSpan.FromSeconds(effectiveSeconds), cancellationToken, _startInfoFactory,
                    _inherited, redirectInput: true, stdin).ConfigureAwait(false);
            }
            catch (CliException exception) when (exception.ExitCode == ExitCode.OperationTimeout)
            { throw new McpCommandException($"The CLI command exceeded the {effectiveSeconds}-second timeout."); }
            catch (CliException exception) { result = TimeoutWorkerSupervisor.RenderError(_host, args, exception); }
            catch (ChildOutputLimitException exception) { throw new McpCommandException(exception.Message); }
            return new McpExecutionResult { ExitCode = result.ExitCode, Stdout = result.Stdout, Stderr = result.Stderr };
        }
        catch (McpCommandException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { throw new McpCommandException("The CLI child process failed unexpectedly."); }
        finally { _gate.Release(); }
    }

    private static ProcessStartInfo CreateStartInfo() =>
        SelfProcessLauncher.CreateBackground("mcp", "Run MCP from the installed aspose-cli executable.");
}

internal sealed class McpCommandException(string message) : Exception(message);
