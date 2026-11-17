using System.CommandLine;
using System.Runtime.CompilerServices;

namespace Aspose.Cli.Host.Invocation;

/// <summary>Host-owned execution policy attached where a command tree is assembled.</summary>
/// <param name="ProductId">The product whose document commands the command belongs to.</param>
/// <param name="OutputBytesLimit">A command-specific output byte limit.</param>
/// <param name="RefusesEvaluationRequest">
/// Whether the command refuses <c>--license-mode evaluation</c> because its effect outlives the
/// command, such as the installed licenses. The refusal comes before any input is read.
/// </param>
internal sealed record CommandInvocationPolicy(
    string? ProductId = null,
    long? OutputBytesLimit = null,
    bool RefusesEvaluationRequest = false);

internal static class CommandInvocationPolicies
{
    private static readonly ConditionalWeakTable<Command, CommandInvocationPolicy> Policies = new();

    internal static Command WithInvocationPolicy(this Command command, CommandInvocationPolicy policy)
    {
        Policies.Add(command, policy);
        return command;
    }

    internal static CommandInvocationPolicy Policy(this Command command) =>
        Policies.TryGetValue(command, out CommandInvocationPolicy? policy) ? policy : new();
}
