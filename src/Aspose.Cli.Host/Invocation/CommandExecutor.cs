using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using Aspose.Cli.Host.Output;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Results;

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

    /// <summary>The handler owns its final deadline check and commits an external process handoff.</summary>
    public int RunHandoff(ParseResult parseResult, GlobalOptions globalOptions,
        Func<CommandContext, ResultEnvelope> handler)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler onCancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += onCancel;
        try
        {
            return Execute(parseResult, globalOptions, admitInputs: true, cancellation.Token, scope =>
            {
                scope.Deadline.ThrowIfExpired("handoff-start");
                ResultEnvelope result = handler(CompositionRoot.Create(_host.Catalog, scope.Globals,
                    deadline: scope.Deadline, resourceBudgets: scope.Budgets));
                return scope.Complete(result, detectPartial: false);
            });
        }
        finally { Console.CancelKeyPress -= onCancel; }
    }

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
                if (!scope.Budgets.HasCommittedOutputs) { scope.Deadline.ThrowIfExpired("command-complete"); }
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
                return scope.Complete(() => Console.Out.WriteLine(result));
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
                    int exitCode = scope.Complete(start.Startup, detectPartial: false);
                    Console.Out.Flush();

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
                    return exitCode;
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
            if (globals.EvaluationRequested
                && parseResult.CommandResult.Command.Policy().RefusesEvaluationRequest)
            {
                throw GlobalOptions.EvaluationRequestRefused(CommandPath(parseResult.CommandResult));
            }
            scope = ExecutionScope.Create(
                _host,
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
                CorruptInputDetection.Explain(
                    _host.EngineFailures.Translate(exception) ?? exception, parseResult, globals, _host.Catalog),
                scope?.Deadline,
                scope?.ResultWritten == true);
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

    /// <summary>
    /// The one failure path. A command answers with one result or one error:
    /// once its result is on stdout, a later failure (a service ending on its
    /// deadline, a failed shutdown) keeps that result and reports itself only
    /// through the exit code and a plain diagnostic line.
    /// </summary>
    private static int HandleFailure(
        IOutputWriter writer,
        GlobalValues globals,
        Stopwatch stopwatch,
        Exception exception,
        OperationDeadline? deadline,
        bool resultWritten)
    {
        if (exception is OperationCanceledException)
        {
            if (deadline?.IsExpired == true)
            {
                exception = CliErrors.OperationTimeout(
                    Math.Max(1, (int)Math.Ceiling(deadline.OriginalBudget?.TotalSeconds ?? 1)),
                    "cooperative-cancellation");
            }
            else if (deadline?.Token.IsCancellationRequested == true)
            {
                // The caller cancelled through the invocation's own token.
                return UserCancelledExitCode;
            }
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
        if (!resultWritten)
        {
            writer.WriteError(error.ToEnvelope());
        }
        else if (!globals.Quiet)
        {
            Console.Error.WriteLine($"aspose-cli: {error.Code.Name}: {error.Message}");
        }
        VerboseLog.Failed(
            globals,
            stopwatch.ElapsedMilliseconds,
            error);
        return (int)error.ExitCode;
    }

    /// <summary>The conventional exit code of a process ended by Ctrl+C.</summary>
    private const int UserCancelledExitCode = 130;

    private static OperationDeadline CreateDeadline(
        GlobalValues globals,
        CancellationToken cancellationToken)
    {
        TimeSpan? budget = ResolveTimeout(globals);
        if (long.TryParse(Environment.GetEnvironmentVariable(WorkerOutputSession.BudgetEnvironmentVariable),
                System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture,
                out long milliseconds) && milliseconds > 0
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
                TimeSpan.FromMilliseconds(milliseconds),
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

    /// <summary>The command's words below the root, for example <c>license install</c>.</summary>
    private static string CommandPath(CommandResult command)
    {
        var names = new Stack<string>();
        for (SymbolResult? current = command; current is CommandResult result && result.Parent is not null;
            current = current.Parent)
        {
            names.Push(result.Command.Name);
        }
        return string.Join(' ', names);
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
            HostContext host,
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
                    CompositionRoot.CreateBudgets(host.Catalog, globals, deadline, host.WorkerOutputs,
                        parseResult.CommandResult.Command.Policy().OutputBytesLimit);
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

        /// <summary>Whether the command's result has reached stdout.</summary>
        public bool ResultWritten { get; private set; }

        /// <summary>
        /// Runs every finalization step that can fail, then writes the result,
        /// so a command never answers with both a result and an error.
        /// </summary>
        public int Complete(
            ResultEnvelope result,
            bool detectPartial)
        {
            if (Globals.EvaluationRequested)
            {
                // Products disclose evaluation mode; only the host knows it was asked for.
                result = result with { Warnings = EnvelopeParts.ForRequestedEvaluation(result.Warnings) };
            }
            int exitCode = Complete(() => _writer.WriteResult(result));
            return detectPartial
                && result is IPartialOutcome { HasFailures: true }
                    ? (int)ExitCode.PartialFailure
                    : exitCode;
        }

        /// <inheritdoc cref="Complete(ResultEnvelope, bool)"/>
        public int Complete(Action writeResult)
        {
            Budgets.ThrowIfFailed();
            Budgets.OutputSession?.SealForPublication();
            ResultWritten = true;
            writeResult();
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
