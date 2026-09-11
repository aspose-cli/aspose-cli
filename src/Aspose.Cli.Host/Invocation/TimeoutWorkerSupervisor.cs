using System.Diagnostics;
using Aspose.Cli.Host.Output;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Host.Invocation;

/// <summary>
/// Supervises commands with a global deadline in a terminable process. The
/// worker can only stage outputs; this parent validates and publishes them
/// after a successful, in-budget exit.
/// </summary>
internal static class TimeoutWorkerSupervisor
{
    private static readonly TimeSpan TerminationGrace = TimeSpan.FromSeconds(5);

    public static int Run(
        HostContext host,
        string[] args,
        ParsedInvocation invocation,
        Func<int> inProcess)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(inProcess);
        if (invocation.GlobalValues?.TimeoutSeconds is not > 0
            || IsWorker() || invocation.ServiceLifetime)
        {
            return inProcess();
        }
        TimeSpan budget = TimeSpan.FromSeconds(invocation.GlobalValues.TimeoutSeconds.Value);

        using var userCancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler onCancel = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            userCancellation.Cancel();
        };
        Console.CancelKeyPress += onCancel;
        try
        {
            return RunWorkerAsync(
                    args,
                    budget,
                    userCancellation.Token,
                    host: host)
                .GetAwaiter()
                .GetResult();
        }
        finally
        {
            Console.CancelKeyPress -= onCancel;
        }
    }

    internal static async Task<int> RunWorkerAsync(
        string[] args,
        TimeSpan budget,
        CancellationToken cancellationToken,
        HostContext host,
        string? executablePath = null,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        string root = PrivateUserStorage.CreateTemporaryDirectory("worker");
        string manifest = Path.Combine(root, "output-manifest.v1.json");
        long expiresAt = checked(
            Environment.TickCount64 + (long)Math.Ceiling(budget.TotalMilliseconds));
        using Process worker = StartWorker(
            args,
            root,
            manifest,
            budget,
            expiresAt,
            executablePath,
            environment,
            host);
        Task<string> standardOutput = worker.StandardOutput.ReadToEndAsync();
        Task<string> standardError = worker.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(budget);
        using var completion = CancellationTokenSource.CreateLinkedTokenSource(
            deadline.Token,
            cancellationToken);
        bool timedOut = false;
        bool cancelled = false;
        try
        {
            await worker.WaitForExitAsync(completion.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (completion.IsCancellationRequested)
        {
            timedOut = deadline.IsCancellationRequested;
            cancelled = cancellationToken.IsCancellationRequested && !timedOut;
        }

        if (timedOut || cancelled)
        {
            return await CompleteInterruptedWorkerAsync(
                worker,
                standardOutput,
                standardError,
                manifest,
                root,
                args,
                budget,
                timedOut,
                host).ConfigureAwait(false);
        }

        string stdout = await standardOutput.ConfigureAwait(false);
        string stderr = await standardError.ConfigureAwait(false);
        int exitCode = worker.ExitCode;
        int? publicationError = CompletePublication(
            manifest,
            root,
            args,
            exitCode,
            host);
        if (publicationError is not null)
        {
            return publicationError.Value;
        }

        DeleteOrPreserve(root);
        Console.Out.Write(stdout);
        Console.Error.Write(stderr);
        return exitCode;
    }

    private static async Task<int> CompleteInterruptedWorkerAsync(
        Process worker,
        Task<string> standardOutput,
        Task<string> standardError,
        string manifest,
        string root,
        string[] args,
        TimeSpan budget,
        bool timedOut,
        HostContext host)
    {
        bool stopped = await TerminateAndConfirmAsync(worker)
            .ConfigureAwait(false);
        await DrainOrCloseAsync(worker, standardOutput, standardError)
            .ConfigureAwait(false);
        CliException timeout = CliErrors.OperationTimeout(
            (int)Math.Ceiling(budget.TotalSeconds),
            "worker-terminated");
        int? recoveryError = stopped
            ? RestoreInterruptedOutputs(
                manifest,
                root,
                args,
                timedOut,
                timeout,
                host)
            : null;
        if (recoveryError is not null)
        {
            return recoveryError.Value;
        }

        if (!stopped)
        {
            return RenderError(
                args,
                CliErrors.WorkerTerminationFailed(worker.Id),
                host);
        }
        DeleteOrPreserve(root);
        return timedOut ? RenderError(args, timeout, host) : 130;
    }

    private static int? RestoreInterruptedOutputs(
        string manifest,
        string root,
        string[] args,
        bool timedOut,
        CliException timeout,
        HostContext host)
    {
        if (!File.Exists(manifest))
        {
            return null;
        }

        try
        {
            WorkerOutputSession.RestoreOrThrow(
                manifest,
                timedOut
                    ? timeout
                    : new OperationCanceledException(
                        "The parent cancelled the supervised worker."));
            return null;
        }
        catch (CliException recoveryFailure)
        {
            return RenderError(args, recoveryFailure, host);
        }
    }

    private static int? CompletePublication(
        string manifest,
        string root,
        string[] args,
        int exitCode,
        HostContext host)
    {
        if (exitCode is (int)ExitCode.Success or (int)ExitCode.PartialFailure)
        {
            try
            {
                if (File.Exists(manifest))
                {
                    WorkerOutputSession.Publish(manifest);
                }
            }
            catch (Exception exception)
            {
                CliException error = exception as CliException
                    ?? CliErrors.Internal(exception, "HOST-WORKER-PUBLISH-0001");
                return RenderError(args, error, host);
            }
        }
        else if (File.Exists(manifest))
        {
            try
            {
                WorkerOutputSession.RestoreOrThrow(
                    manifest,
                    new IOException(
                        "The worker command failed after staging outputs."));
            }
            catch (CliException recoveryFailure)
            {
                return RenderError(args, recoveryFailure, host);
            }
        }
        return null;
    }

    private static Process StartWorker(
        IReadOnlyList<string> args,
        string root,
        string manifest,
        TimeSpan budget,
        long expiresAt,
        string? executablePath,
        IReadOnlyDictionary<string, string?>? environment,
        HostContext host)
    {
        string processPath = executablePath
            ?? Environment.ProcessPath
            ?? throw new InvalidOperationException("The current executable path is unavailable.");
        var start = new ProcessStartInfo
        {
            FileName = processPath,
            WorkingDirectory = Directory.GetCurrentDirectory(),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        if (Path.GetFileNameWithoutExtension(processPath)
            .Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            start.ArgumentList.Add(Environment.GetCommandLineArgs()[0]);
        }

        foreach (string argument in args)
        {
            start.ArgumentList.Add(argument);
        }

        InvocationEnvironment.Configure(start, host.Parser.Parse(args.ToArray()),
            InvocationEnvironment.ProductVariables(host.Catalog));
        if (environment is not null)
        {
            foreach ((string name, string? value) in environment)
            {
                start.Environment[name] = value;
            }
        }
        start.Environment[WorkerOutputSession.WorkerEnvironmentVariable] = "1";
        start.Environment[WorkerOutputSession.RootEnvironmentVariable] = root;
        start.Environment[WorkerOutputSession.ManifestEnvironmentVariable] = manifest;
        start.Environment[WorkerOutputSession.DeadlineEnvironmentVariable] =
            expiresAt.ToString(System.Globalization.CultureInfo.InvariantCulture);
        start.Environment[WorkerOutputSession.BudgetEnvironmentVariable] =
            Math.Ceiling(budget.TotalMilliseconds)
                .ToString(System.Globalization.CultureInfo.InvariantCulture);

        return Process.Start(start)
            ?? throw new InvalidOperationException("The timeout worker could not be started.");
    }

    private static async Task<bool> TerminateAndConfirmAsync(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (Exception exception) when (
            exception is System.ComponentModel.Win32Exception
                or NotSupportedException)
        {
            return false;
        }

        using var grace = new CancellationTokenSource(TerminationGrace);
        try
        {
            await process.WaitForExitAsync(grace.Token).ConfigureAwait(false);
            return process.HasExited;
        }
        catch (OperationCanceledException)
        {
            return process.HasExited;
        }
    }

    private static int RenderError(
        IReadOnlyList<string> args,
        CliException error,
        HostContext host)
    {
        (OutputMode output, bool quiet) =
            host.Parser.ResolveErrorOutput(args);
        IOutputWriter writer = OutputWriterFactory.Create(
            output,
            quiet,
            host.Catalog,
            host.ContractJson.Serializer);
        writer.WriteError(error.ToEnvelope());
        return (int)error.ExitCode;
    }

    private static bool IsWorker() =>
        string.Equals(
            Environment.GetEnvironmentVariable(
                WorkerOutputSession.WorkerEnvironmentVariable),
            "1",
            StringComparison.Ordinal);

    private static void DeleteOrPreserve(string root)
    {
        if (PrivateUserStorage.TryDeleteTree(root))
        {
            return;
        }
        Trace.TraceWarning(
            "Worker staging cleanup preserved a changed or unverified root: '{0}'.",
            root);
    }

    private static async Task DrainOrCloseAsync(
        Process process,
        Task<string> standardOutput,
        Task<string> standardError)
    {
        try
        {
            await Task.WhenAll(standardOutput, standardError)
                .WaitAsync(TerminationGrace)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            process.StandardOutput.Dispose();
            process.StandardError.Dispose();
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
