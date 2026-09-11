using System.CommandLine;
using System.Runtime.CompilerServices;

namespace Aspose.Cli.Host.Invocation;

/// <summary>Host-owned execution policy attached where a command tree is assembled.</summary>
internal sealed record CommandInvocationPolicy(
    string? ProductId = null, bool ServiceLifetime = false, bool McpReadOnly = false);

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
