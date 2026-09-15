using System.Buffers;
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
internal sealed class InvocationProcessException(string message) : Exception(message);

/// <summary>CLI and MCP share one deadline, terminable producer and parent-owned publication.</summary>
internal static class TimeoutWorkerSupervisor
{
    internal static readonly TimeSpan TerminationGrace = TimeSpan.FromSeconds(2);
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
            || host.WorkerOutputs is not null || invocation.ServiceLifetime) { return inProcess(); }
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
            catch (OperationCanceledException) { return 130; }
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
        string root = PrivateUserStorage.CreateTemporaryDirectory("worker");
        string manifest = Path.Combine(root, WorkerOutputSession.ManifestName);
        bool safeToClean = true;
        try
        {
            using var channel = new InvocationInputServer();
            ProcessStartInfo start = startInfoFactory();
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = start.RedirectStandardError = true;
            start.RedirectStandardInput = redirectInput;
            start.StandardOutputEncoding = start.StandardErrorEncoding = Encoding.UTF8;
            if (redirectInput) { start.StandardInputEncoding = new UTF8Encoding(false); }
            start.WorkingDirectory = Path.GetFullPath(invocation.GlobalValues?.WorkDir
                ?? inherited?.WorkDir ?? Directory.GetCurrentDirectory());
            InvocationEnvironment.Configure(start, invocation, InvocationEnvironment.ProductVariables(host.Catalog));
            foreach (string argument in args) { start.ArgumentList.Add(argument); }
            start.Environment[WorkerOutputSession.WorkerEnvironmentVariable] = "1";
            start.Environment[WorkerOutputSession.RootEnvironmentVariable] = root;
            start.Environment[WorkerOutputSession.ManifestEnvironmentVariable] = manifest;
            start.Environment[WorkerOutputSession.DeadlineEnvironmentVariable] = deadline.ExpiresAtTick!.Value.ToString(CultureInfo.InvariantCulture);
            start.Environment[WorkerOutputSession.BudgetEnvironmentVariable] = Math.Ceiling(budget.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
            channel.Configure(start);
            deadline.ThrowIfExpired("worker-start");
            using Process process = Process.Start(start) ?? throw new IOException("The CLI child process could not be started.");
            using IDisposable? job = WindowsProcessJob.TryAttach(process);
            Task startup = channel.ServeAsync(inherited, deadline.Token);
            Task input = redirectInput ? WriteInputAsync(process, stdin, deadline.Token) : Task.CompletedTask;
            Task<string> stdout = ReadBoundedAsync(process.StandardOutput.BaseStream, "stdout", deadline.Token);
            Task<string> stderr = ReadBoundedAsync(process.StandardError.BaseStream, "stderr", deadline.Token);
            try
            {
                // Observe individual faults immediately: a full pipe must not leave the producer blocked.
                await AwaitTasksAsync([startup, process.WaitForExitAsync(deadline.Token), input, stdout, stderr], deadline.Token)
                    .ConfigureAwait(false);
            }
            catch
            {
                safeToClean = await StopAsync(process, job, [startup, input, stdout, stderr]).ConfigureAwait(false);
                if (!safeToClean) { throw CliErrors.WorkerTerminationFailed(process.Id); }
                deadline.ThrowIfExpired("worker-terminated");
                throw;
            }
            deadline.ThrowIfExpired("worker-exited");
            int exitCode = process.ExitCode;
            if (exitCode is (int)ExitCode.Success or (int)ExitCode.PartialFailure && File.Exists(manifest))
            {
                // This is the only final publisher. Recovery runs here even when the deadline expires.
                WorkerOutputSession.Publish(manifest, CompositionRoot.CreateBudgets(host.Catalog,
                    invocation.GlobalValues ?? inherited ?? throw new InvalidDataException("The publishing invocation has no global values."), deadline));
            }
            return new InvocationProcessResult(exitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
        }
        finally
        {
            if (safeToClean && !PrivateUserStorage.TryDeleteTree(root))
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

    private static async Task AwaitTasksAsync(IReadOnlyList<Task> tasks, CancellationToken token)
    {
        var pending = tasks.ToList();
        while (pending.Count > 0)
        {
            Task completed = await Task.WhenAny(pending).WaitAsync(token).ConfigureAwait(false);
            pending.Remove(completed);
            await completed.ConfigureAwait(false);
        }
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

    private static async Task<string> ReadBoundedAsync(Stream stream, string name, CancellationToken token)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            using var output = new MemoryStream();
            while (true)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false);
                if (read == 0) { return Encoding.UTF8.GetString(output.GetBuffer(), 0, (int)output.Length); }
                if (output.Length + read > MaximumOutputBytes)
                { throw new InvocationProcessException($"The CLI {name} exceeded the {MaximumOutputBytes}-byte limit."); }
                output.Write(buffer, 0, read);
            }
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    private static async Task<bool> StopAsync(Process process, IDisposable? job, Task[] tasks)
    {
        try { if (!process.HasExited) { process.Kill(entireProcessTree: true); } }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
        job?.Dispose();
        if (process.StartInfo.RedirectStandardInput) { process.StandardInput.Dispose(); }
        process.StandardOutput.Dispose();
        process.StandardError.Dispose();
        using var grace = new CancellationTokenSource(TerminationGrace);
        try { await process.WaitForExitAsync(grace.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) { return process.HasExited; }
        // Observe pipe failures without extending the termination budget.
        _ = Task.WhenAll(tasks).ContinueWith(static task => { _ = task.Exception; },
            CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return process.HasExited;
    }
}
