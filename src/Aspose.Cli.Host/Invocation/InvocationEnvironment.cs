using Aspose.Cli.Sdk.IO.Budgets;

namespace Aspose.Cli.Host.Invocation;

/// <summary>The environment variables a command reads.</summary>
internal static class InvocationEnvironment
{
    internal static Func<string, string?> CreateSecretReader(ResourceBudgetLedger budgets) =>
        new EnvironmentSecrets(Environment.GetEnvironmentVariable, budgets).Read;
}
