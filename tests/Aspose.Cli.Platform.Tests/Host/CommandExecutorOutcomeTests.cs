using System.CommandLine;
using System.Globalization;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.Commands;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Xunit;

namespace Aspose.Cli.Host.Tests;

/// <summary>A command answers with exactly one result or one error, never both.</summary>
[Collection("Console capture")]
public sealed class CommandExecutorOutcomeTests
{
    [Fact]
    public void HostedService_EndingOnItsDeadline_KeepsItsStartupResultAsTheOnlyEnvelope()
    {
        Outcome outcome = Invoke((executor, globals) => new Command("hosted-test")
            .WithAction(parse => executor.RunHosted(parse, globals, _ => new HostedCommandLifecycle(
                new ProductPreviewStatusResult { Sessions = [] },
                static (_, _) => WaitOutcome.DeadlineExpired,
                static () => { }))),
            "hosted-test", "--timeout", "30", "--output", "json");

        Assert.Equal((int)ExitCode.OperationTimeout, outcome.ExitCode);
        Assert.NotNull(JsonNode.Parse(outcome.StdOut)!["sessions"]);
        Assert.DoesNotContain("\"error\"", outcome.StdErr, StringComparison.Ordinal);
        Assert.Contains(ErrorCodes.OperationTimeout.Name, outcome.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public void ForeignCancellation_IsAnInternalErrorWithAnEnvelope()
    {
        Outcome outcome = Invoke((executor, globals) => new Command("cancel-test")
            .WithAction(parse => executor.RunLightweight(parse, globals,
                static (_, _) => throw new TaskCanceledException("an SDK call gave up"))),
            "cancel-test", "--output", "json");

        Assert.Equal((int)ExitCode.Internal, outcome.ExitCode);
        Assert.Empty(outcome.StdOut);
        Assert.Equal(
            ErrorCodes.Internal.Name,
            JsonNode.Parse(outcome.StdErr)!["error"]!["code"]!.GetValue<string>());
    }

    private static Outcome Invoke(
        Func<CommandExecutor, GlobalOptions, Command> command,
        params string[] arguments)
    {
        HostContext host = ActualCommandTree.Host;
        RootCommand root = RootCommandFactory.Create(host, out GlobalOptions globals);
        root.Subcommands.Add(command(new CommandExecutor(host), globals));
        TextWriter originalOutput = Console.Out;
        TextWriter originalError = Console.Error;
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            int exitCode = root.Parse(arguments).Invoke();
            return new Outcome(exitCode, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(originalOutput);
            Console.SetError(originalError);
        }
    }

    private sealed record Outcome(int ExitCode, string StdOut, string StdErr);
}

internal static class CommandActionExtensions
{
    public static Command WithAction(this Command command, Func<ParseResult, int> action)
    {
        command.SetAction(action);
        return command;
    }
}
