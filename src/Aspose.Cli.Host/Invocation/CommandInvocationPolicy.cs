using System.CommandLine;
using System.Runtime.CompilerServices;

namespace Aspose.Cli.Host.Invocation;

/// <summary>Host-owned execution policy attached where a command tree is assembled.</summary>
internal enum CommandExecutionOwnership { Worker, Service, ParentHandoff }

/// <param name="ProductId">The product whose document commands the command belongs to.</param>
/// <param name="Execution">Who owns the process that runs the command.</param>
/// <param name="McpReadOnly">
/// Whether MCP <c>execute</c> may run this host command. Every product command
/// is available through MCP; of the host commands, only those that neither
/// change user state nor start or stop anything are: <c>doctor</c>,
/// <c>schema</c>, <c>docs</c>, <c>fonts list</c>, <c>fonts check</c>,
/// <c>license status</c>, <c>skill list</c>, <c>preview status</c> and
/// <c>app status</c>. <c>capabilities</c> has its own MCP tool.
/// </param>
/// <param name="EnvironmentVariables">Extra environment variables a supervised worker receives.</param>
/// <param name="OutputBytesLimit">A command-specific output byte limit.</param>
internal sealed record CommandInvocationPolicy(
    string? ProductId = null,
    CommandExecutionOwnership Execution = CommandExecutionOwnership.Worker,
    bool McpReadOnly = false,
    IReadOnlyList<string>? EnvironmentVariables = null,
    long? OutputBytesLimit = null);

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
