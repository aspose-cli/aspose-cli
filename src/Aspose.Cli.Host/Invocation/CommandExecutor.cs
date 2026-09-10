using System.CommandLine;
using System.Diagnostics;
using Aspose.Cli.Host.Output;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Host.Invocation;

/// <summary>
/// Executes every command through one scope for output, deadline, resource
/// admission, diagnostics, and cleanup.
/// </summary>
internal sealed class CommandExecutor
{
    private readonly HostContext _host;

    public CommandExecutor(HostContext host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public int RunLightweight(
        ParseResult parseResult,
        GlobalOptions globalOptions,
        Func<GlobalValues, ResourceBudgetLedger, ResultEnvelope> handler)
        => RunEnvelope(
            parseResult,
            globalOptions,
            scope => handler(scope.Globals, scope.Budgets),
            detectPartial: false);

    public int Run(
        ParseResult parseResult,
        GlobalOptions globalOptions,
        Func<CommandContext, ResultEnvelope> handler)
        => RunEnvelope(
            parseResult,
            globalOptions,
            scope => handler(CompositionRoot.Create(
                    _host.Catalog,
                    scope.Globals,
                    deadline: scope.Deadline,
                    resourceBudgets: scope.Budgets)),
            detectPartial: true);

    public int RunProduct<TPort>(
        ParseResult parseResult,
        GlobalOptions globalOptions,
        string productId,
        Func<ProductCommandContext<TPort>, ResultEnvelope> handler)
        where TPort : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        return RunEnvelope(
            parseResult,
            globalOptions,
            scope => handler(CompositionRoot.CreateProduct<TPort>(
                        _host.Catalog,
                        productId,
                        scope.Globals,
                        scope.Deadline,
                        scope.Budgets)),
            detectPartial: true);
    }

    private int RunEnvelope(
        ParseResult parseResult,
        GlobalOptions globalOptions,
        Func<ExecutionScope, ResultEnvelope> handler,
        bool detectPartial)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Execute(
            parseResult,
            globalOptions,
            admitInputs: true,
            cancellationToken: default,
            scope =>
            {
                scope.Deadline.ThrowIfExpired("command-start");
                ResultEnvelope result = handler(scope);
                scope.Deadline.ThrowIfExpired("command-complete");
                return scope.Complete(result, detectPartial);
            });
    }

    public int RunRaw(
        ParseResult parseResult,
        GlobalOptions globalOptions,
        Func<string> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Execute(
            parseResult,
            globalOptions,
            admitInputs: false,
            cancellationToken: default,
            scope =>
            {
                scope.Deadline.ThrowIfExpired("raw-start");
                string result = handler();
                scope.Deadline.ThrowIfExpired("raw-complete");
                Console.Out.WriteLine(result);
                return scope.Complete();
            });
    }

    public int RunHosted(
        ParseResult parseResult,
        GlobalOptions globalOptions,
        Func<GlobalValues, HostedCommandLifecycle> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return RunService(
            parseResult,
            globalOptions,
            scope => handler(scope.Globals));
    }

    public int RunServer(
        ParseResult parseResult,
        GlobalOptions globalOptions,
        Func<CommandContext, HostedCommandLifecycle> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return RunService(
            parseResult,
            globalOptions,
            scope =>
            {
                CommandContext context = CompositionRoot.Create(
                    _host.Catalog,
                    scope.Globals,
                    deadline: scope.Deadline,
                    resourceBudgets: scope.Budgets);
                return handler(context);
            });
    }

    private int RunService(
        ParseResult parseResult,
        GlobalOptions globalOptions,
        Func<ExecutionScope, HostedCommandLifecycle> handler) =>
        Execute(
            parseResult,
            globalOptions,
            admitInputs: true,
            cancellationToken: default,
            scope =>
            {
                HostedCommandLifecycle? start = null;
                try
                {
                    scope.Deadline.ThrowIfExpired("server-start");
                    start = handler(scope);
                    scope.Deadline.ThrowIfExpired("server-handshake");
                    scope.Write(start.Startup);
                    Console.Out.Flush();

                    if (!start.Once)
                    {
                        WaitOutcome outcome = WaitForShutdownSignal(
                            cancellationToken => start.Wait(
                                scope.Deadline.Remaining,
                                cancellationToken));
                        if (outcome == WaitOutcome.DeadlineExpired
                            && scope.Deadline.OriginalBudget is { } budget)
                        {
                            throw CliErrors.OperationTimeout(
                                (int)budget.TotalSeconds);
                        }
                        if (outcome == WaitOutcome.IdleExpired
                            && !scope.Globals.Quiet)
                        {
                            Console.Error.WriteLine(
                                "preview: no clients and no render activity within the idle window; shutting down.");
                        }
                    }

                    return scope.Complete();
                }
                finally
                {
                    start?.Shutdown();
                }
            });

    private int Execute(
        ParseResult parseResult,
        GlobalOptions globalOptions,
        bool admitInputs,
        CancellationToken cancellationToken,
        Func<ExecutionScope, int> action)
    {
        ArgumentNullException.ThrowIfNull(parseResult);
        ArgumentNullException.ThrowIfNull(globalOptions);
        ArgumentNullException.ThrowIfNull(action);

        GlobalValues globals = globalOptions.Resolve(parseResult);
        IOutputWriter writer = OutputWriterFactory.Create(
            globals.Output,
            globals.Quiet,
            _host.Catalog,
            _host.ContractJson.Serializer);
        var stopwatch = Stopwatch.StartNew();
        ExecutionScope? scope = null;
        try
        {
            scope = ExecutionScope.Create(
                _host.Catalog,
                parseResult,
                globals,
                writer,
                stopwatch,
                admitInputs,
                cancellationToken);
            return action(scope);
        }
        catch (Exception exception)
        {
            return HandleFailure(
                writer,
                globals,
                stopwatch,
                exception);
        }
        finally
        {
            scope?.Dispose();
        }
    }

    private static WaitOutcome WaitForShutdownSignal(
        Func<CancellationToken, WaitOutcome> wait)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler onCancel = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            try
            {
                cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Another exit condition already completed the wait.
            }
        };
        Console.CancelKeyPress += onCancel;
        try
        {
            return wait(cancellation.Token);
        }
        finally
        {
            Console.CancelKeyPress -= onCancel;
        }
    }

    private static int HandleFailure(
        IOutputWriter writer,
        GlobalValues globals,
        Stopwatch stopwatch,
        Exception exception)
    {
        if (exception is OperationCanceledException
            && globals.TimeoutSeconds is > 0)
        {
            exception = CliErrors.OperationTimeout(
                globals.TimeoutSeconds.Value,
                "cooperative-cancellation");
        }

        bool expected = exception is CliException;
        if (!expected)
        {
            ProcessFailureLog.Write("command", exception);
        }

        CliException error = expected
            ? (CliException)exception
            : CliErrors.Internal(
                exception,
                HostDiagnosticIds.CommandUnhandled);
        writer.WriteError(error.ToEnvelope());
        VerboseLog.Failed(
            globals,
            stopwatch.ElapsedMilliseconds,
            error);
        return (int)error.ExitCode;
    }

    private static OperationDeadline CreateDeadline(
        GlobalValues globals,
        CancellationToken cancellationToken)
    {
        TimeSpan? budget = ResolveTimeout(globals);
        if (budget is { } original
            && long.TryParse(
                Environment.GetEnvironmentVariable(
                    WorkerOutputSession.DeadlineEnvironmentVariable),
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out long expiresAt)
            && string.Equals(
                Environment.GetEnvironmentVariable(
                    WorkerOutputSession.WorkerEnvironmentVariable),
                "1",
                StringComparison.Ordinal))
        {
            return OperationDeadline.FromAbsoluteTick(
                original,
                expiresAt,
                cancellationToken);
        }
        return OperationDeadline.Start(budget, cancellationToken);
    }

    private static TimeSpan? ResolveTimeout(GlobalValues globals)
    {
        if (globals.TimeoutSeconds is not { } seconds)
        {
            return null;
        }
        if (seconds <= 0)
        {
            throw CliErrors.OptionInvalid(
                "--timeout",
                "value must be a positive number of seconds",
                "Pass a timeout like --timeout 30, or omit it to run unbounded.");
        }
        return TimeSpan.FromSeconds(seconds);
    }

    private sealed class ExecutionScope : IDisposable
    {
        private readonly IOutputWriter _writer;
        private readonly Stopwatch _stopwatch;
        private ExecutionScope(
            GlobalValues globals,
            IOutputWriter writer,
            Stopwatch stopwatch,
            OperationDeadline deadline,
            ResourceBudgetLedger budgets)
        {
            Globals = globals;
            _writer = writer;
            _stopwatch = stopwatch;
            Deadline = deadline;
            Budgets = budgets;
        }

        public GlobalValues Globals { get; }

        public OperationDeadline Deadline { get; }

        public ResourceBudgetLedger Budgets { get; }

        public static ExecutionScope Create(
            ProductCatalog catalog,
            ParseResult parseResult,
            GlobalValues globals,
            IOutputWriter writer,
            Stopwatch stopwatch,
            bool admitInputs,
            CancellationToken cancellationToken)
        {
            OperationDeadline deadline =
                CreateDeadline(globals, cancellationToken);
            try
            {
                ResourceBudgetLedger budgets =
                    CompositionRoot.CreateBudgets(catalog, globals, deadline);
                if (admitInputs)
                {
                    ProductInputAdmission.Admit(
                        parseResult,
                        globals,
                        budgets);
                }
                return new ExecutionScope(
                    globals,
                    writer,
                    stopwatch,
                    deadline,
                    budgets);
            }
            catch
            {
                deadline.Dispose();
                throw;
            }
        }

        public void Write(ResultEnvelope result) =>
            _writer.WriteResult(result);

        public int Complete(
            ResultEnvelope result,
            bool detectPartial)
        {
            Write(result);
            Complete();
            return detectPartial
                && result is IPartialOutcome { HasFailures: true }
                    ? (int)ExitCode.PartialFailure
                    : (int)ExitCode.Success;
        }

        public int Complete()
        {
            VerboseLog.Completed(
                Globals,
                _stopwatch.ElapsedMilliseconds);
            return (int)ExitCode.Success;
        }

        public void Dispose()
        {
            Deadline.Dispose();
        }
    }

}
