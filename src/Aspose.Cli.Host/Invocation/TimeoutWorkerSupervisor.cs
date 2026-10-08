using System.Diagnostics;
using System.Globalization;
using System.Text;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.Output;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Host.Invocation;

internal sealed record InvocationProcessResult(int ExitCode, string Stdout, string Stderr);

/// <summary>CLI and MCP share one deadline, terminable producer and parent-owned publication.</summary>
internal static class TimeoutWorkerSupervisor
{
    internal const int MaximumOutputBytes = 4 * 1024 * 1024;

    internal static WorkerOutputSession? ReceiveOutputSession() =>
        Environment.GetEnvironmentVariable(WorkerOutputSession.WorkerEnvironmentVariable) == "1"
            ? new WorkerOutputSession(
                Environment.GetEnvironmentVariable(WorkerOutputSession.RootEnvironmentVariable)
                    ?? throw new InvalidDataException("The worker output root is missing."),
                Environment.GetEnvironmentVariable(WorkerOutputSession.ManifestEnvironmentVariable)
                    ?? throw new InvalidDataException("The worker output manifest is missing."))
            : null;

    public static int Run(HostContext host, string[] args, ParsedInvocation invocation, Func<int> inProcess)
    {
        if (invocation.GlobalValues?.TimeoutSeconds is not > 0
            || host.WorkerOutputs is not null || invocation.Execution != CommandExecutionOwnership.Worker) { return inProcess(); }
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler onCancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += onCancel;
        try
        {
            InvocationProcessResult result;
            try
            {
                result = ExecuteAsync(host, args, invocation,
                    TimeSpan.FromSeconds(invocation.GlobalValues.TimeoutSeconds.Value), cancellation.Token,
                    () => SelfProcessLauncher.CreateBackground("--timeout", "Run from the installed aspose-cli executable."),
                    InvocationInputs.Current?.Inherited).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return 130; }
            catch (Exception error) { result = RenderError(host, args, error); }
            Console.Out.Write(result.Stdout);
            Console.Error.Write(result.Stderr);
            return result.ExitCode;
        }
        finally { Console.CancelKeyPress -= onCancel; }
    }

    internal static async Task<InvocationProcessResult> ExecuteAsync(
        HostContext host, string[] args, ParsedInvocation invocation, TimeSpan budget,
        CancellationToken cancellationToken, Func<ProcessStartInfo> startInfoFactory,
        GlobalValues? inherited = null, bool redirectInput = false, string? stdin = null)
    {
        using OperationDeadline deadline = OperationDeadline.Start(budget, cancellationToken);
        return await ExecuteAsync(host, args, invocation, deadline, startInfoFactory,
            inherited, redirectInput, stdin).ConfigureAwait(false);
    }

    /// <summary>Runs owned preparation against the caller's existing absolute deadline.</summary>
    internal static async Task<InvocationProcessResult> ExecuteAsync(
        HostContext host, string[] args, ParsedInvocation invocation, OperationDeadline deadline,
        Func<ProcessStartInfo> startInfoFactory, GlobalValues? inherited = null,
        bool redirectInput = false, string? stdin = null)
    {
        string root = UserStorage.CreateTemporaryDirectory("worker");
        string manifest = Path.Combine(root, WorkerOutputSession.ManifestName);
        bool safeToClean = true;
        try
        {
            using var channel = new InvocationInputServer();
            ProcessStartInfo start = startInfoFactory();
            start.RedirectStandardInput = redirectInput;
            start.StandardOutputEncoding = start.StandardErrorEncoding = Encoding.UTF8;
            if (redirectInput) { start.StandardInputEncoding = new UTF8Encoding(false); }
            // Raw arguments are parsed again by the child against the original base.
            start.WorkingDirectory = Path.GetFullPath(inherited?.WorkDir ?? Directory.GetCurrentDirectory());
            InvocationEnvironment.Configure(start, invocation, InvocationEnvironment.ProductVariables(host.Catalog));
            foreach (string argument in args) { start.ArgumentList.Add(argument); }
            start.Environment[WorkerOutputSession.WorkerEnvironmentVariable] = "1";
            start.Environment[WorkerOutputSession.RootEnvironmentVariable] = root;
            start.Environment[WorkerOutputSession.ManifestEnvironmentVariable] = manifest;
            if (deadline.ExpiresAtTick is { } expires)
            {
                start.Environment[WorkerOutputSession.DeadlineEnvironmentVariable] = expires.ToString(CultureInfo.InvariantCulture);
                start.Environment[WorkerOutputSession.BudgetEnvironmentVariable] = Math.Ceiling(deadline.OriginalBudget!.Value.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
            }
            channel.Configure(start);
            deadline.ThrowIfExpired("worker-start");
            ChildProcessResult child;
            try
            {
                child = await ChildProcess.RunAsync(
                    start,
                    MaximumOutputBytes,
                    deadline.Token,
                    (process, token) =>
                    [
                        channel.ServeAsync(inherited, token),
                        redirectInput ? WriteInputAsync(process, stdin, token) : Task.CompletedTask,
                    ]).ConfigureAwait(false);
            }
            catch (CliException exception) when (exception.Code == ErrorCodes.WorkerTerminationFailed)
            {
                // A producer that may still write keeps its staging.
                safeToClean = false;
                throw;
            }
            catch
            {
                deadline.ThrowIfExpired("worker-terminated");
                throw;
            }
            deadline.ThrowIfExpired("worker-exited");
            int exitCode = child.ExitCode;
            if (exitCode is (int)ExitCode.Success or (int)ExitCode.PartialFailure && File.Exists(manifest))
            {
                // This is the only final publisher. Recovery runs here even when the deadline expires.
                WorkerOutputPublisher.Publish(manifest, CompositionRoot.CreateBudgets(host.Catalog,
                    invocation.GlobalValues ?? inherited ?? throw new InvalidDataException("The publishing invocation has no global values."), deadline,
                    outputBytesLimit: invocation.Command.Policy().OutputBytesLimit));
            }
            return new InvocationProcessResult(
                exitCode, Encoding.UTF8.GetString(child.Stdout), Encoding.UTF8.GetString(child.Stderr));
        }
        finally
        {
            if (safeToClean && !UserStorage.TryDeleteTree(root))
            { Trace.TraceWarning("Worker cleanup preserved changed or unverified staging: '{0}'.", root); }
        }
    }

    internal static InvocationProcessResult RenderError(HostContext host, IReadOnlyList<string> args, Exception exception)
    {
        CliException error = exception as CliException ?? CliErrors.Internal(exception, "HOST-WORKER-0001");
        (OutputMode mode, bool quiet) = host.Parser.ResolveErrorOutput(args);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var diagnostic = new StringWriter(CultureInfo.InvariantCulture);
        IOutputWriter writer = OutputWriterFactory.Create(mode, quiet, host.Catalog,
            host.ContractJson.Serializer, output, diagnostic);
        writer.WriteError(error.ToEnvelope());
        return new InvocationProcessResult((int)error.ExitCode, output.ToString(), diagnostic.ToString());
    }

    private static async Task WriteInputAsync(Process process, string? input, CancellationToken token)
    {
        try
        {
            if (input is not null) { await process.StandardInput.WriteAsync(input.AsMemory(), token).ConfigureAwait(false); }
        }
        catch (IOException) when (process.HasExited) { }
        catch (ObjectDisposedException) when (process.HasExited) { }
        finally { process.StandardInput.Close(); }
    }

}
