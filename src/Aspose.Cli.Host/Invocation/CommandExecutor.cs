using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Aspose.Cli.Host.Output;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Input;

namespace Aspose.Cli.Host.Invocation;

/// <summary>
/// Executes every command through one scope for output, cancellation, resource
/// admission and diagnostics.
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
            scope => handler(scope.Globals, scope.Budgets));

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
                    resourceBudgets: scope.Budgets)));

    /// <summary>
    /// Runs a command that answers with one result. Ctrl+C cancels it as the caller's own
    /// cancellation: the work stops at its next check, nothing is published, its staging is
    /// removed and the process exits 130. A second Ctrl+C ends the process at once.
    /// </summary>
    private int RunEnvelope(
        ParseResult parseResult,
        GlobalOptions globalOptions,
        Func<ExecutionScope, ResultEnvelope> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return ConsoleInterrupt.Run(
            cancellationToken => Execute(
                parseResult,
                globalOptions,
                admitInputs: true,
                cancellationToken,
                scope =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ResultEnvelope result = handler(scope);
                    if (!scope.Budgets.HasCommittedOutputs) { cancellationToken.ThrowIfCancellationRequested(); }
                    return scope.Complete(result);
                }));
    }

    /// <summary>
    /// Runs a command that prints a raw document rather than an envelope. A JSON document
    /// prints on one line with <c>--output-format compact</c>, as an envelope does, and
    /// indented otherwise.
    /// </summary>
    public int RunRaw(
        ParseResult parseResult,
        GlobalOptions globalOptions,
        Func<string> handler,
        bool json = false)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Execute(
            parseResult,
            globalOptions,
            admitInputs: false,
            cancellationToken: default,
            scope =>
            {
                string result = handler();
                if (json && scope.Globals.OutputFormat == OutputMode.Compact)
                {
                    result = Compact(result);
                }

                return scope.Complete(() => Console.Out.WriteLine(result));
            });
    }

    /// <summary>A JSON document on one line, with the same values and the same characters unescaped.</summary>
    private static string Compact(string document)
    {
        using JsonDocument parsed = JsonDocument.Parse(document);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            parsed.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

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
        var writer = new JsonOutputWriter(_host.ContractJson, globals.OutputFormat);
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
                _host.EngineFailures.Translate(exception) ?? exception,
                cancellationToken,
                scope?.ResultWritten == true);
        }
    }

    /// <summary>
    /// The one failure path. A command answers with one result or one error:
    /// once it has begun writing its result to stdout, a failure (of that write,
    /// or of the verbose line after it) adds no error envelope and reports itself
    /// only through the exit code and a plain diagnostic line.
    /// </summary>
    private static int HandleFailure(
        JsonOutputWriter writer,
        GlobalValues globals,
        Stopwatch stopwatch,
        Exception exception,
        CancellationToken cancellation,
        bool resultWritten)
    {
        if (exception is OperationCanceledException
            && cancellation.IsCancellationRequested)
        {
            // The caller cancelled through the invocation's own token.
            return (int)ExitCode.Cancelled;
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
        else
        {
            Console.Error.WriteLine($"aspose-cli: {error.Code.Name}: {error.Message}");
        }
        VerboseLog.Failed(
            globals,
            stopwatch.ElapsedMilliseconds,
            error);
        return (int)error.ExitCode;
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

    private sealed class ExecutionScope
    {
        private readonly JsonOutputWriter _writer;
        private readonly Stopwatch _stopwatch;
        private ExecutionScope(
            GlobalValues globals,
            JsonOutputWriter writer,
            Stopwatch stopwatch,
            ResourceBudgetLedger budgets)
        {
            Globals = globals;
            _writer = writer;
            _stopwatch = stopwatch;
            Budgets = budgets;
        }

        public GlobalValues Globals { get; }

        public ResourceBudgetLedger Budgets { get; }

        public static ExecutionScope Create(
            HostContext host,
            ParseResult parseResult,
            GlobalValues globals,
            JsonOutputWriter writer,
            Stopwatch stopwatch,
            bool admitInputs,
            CancellationToken cancellationToken)
        {
            ResourceBudgetLedger budgets =
                CompositionRoot.CreateBudgets(host.Catalog, globals, cancellationToken,
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
                budgets);
        }

        /// <summary>Whether the command's result has reached stdout.</summary>
        public bool ResultWritten { get; private set; }

        /// <summary>
        /// Runs every finalization step that can fail, then writes the result,
        /// so a command never answers with both a result and an error.
        /// </summary>
        public int Complete(ResultEnvelope result)
        {
            if (NetworkGate.Warning is { } blocked)
            {
                result = result with { Warnings = [.. result.Warnings ?? [], blocked] };
            }

            return Complete(() => _writer.WriteResult(result));
        }

        /// <inheritdoc cref="Complete(ResultEnvelope)"/>
        public int Complete(Action writeResult)
        {
            Budgets.ThrowIfFailed();
            ResultWritten = true;
            writeResult();
            VerboseLog.Completed(
                Globals,
                _stopwatch.ElapsedMilliseconds);
            return (int)ExitCode.Success;
        }
    }

}
