using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Sdk.Execution;

/// <summary>Invocation-scoped, bounded secret lookup through an explicitly supplied environment source.</summary>
public sealed class EnvironmentSecrets(Func<string, string?> source, ResourceBudgetLedger budgets)
{
    private readonly Func<string, string?> _source = source ?? throw new ArgumentNullException(nameof(source));
    private readonly ResourceBudgetLedger _budgets = budgets ?? throw new ArgumentNullException(nameof(budgets));
    private readonly Dictionary<string, string?> _values = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private readonly object _gate = new();

    /// <summary>Returns a named value once per invocation; missing and empty values remain distinct.</summary>
    public string? Read(string name)
    {
        if (string.IsNullOrEmpty(name) || name.IndexOfAny(['\0', '=']) >= 0)
        {
            throw CliErrors.OptionInvalid("environment variable", "the name is invalid",
                "Use a non-empty environment variable name without NUL or '='.");
        }
        lock (_gate)
        {
            _budgets.Deadline.ThrowIfExpired("environment-secret");
            if (_values.TryGetValue(name, out string? cached)) { return cached; }
            string? value = _source(name);
            _budgets.Consume(ResourceBudgetKinds.SecretCharacters, value?.Length ?? 0,
                "characters", "environment-secret");
            _values.Add(name, value);
            return value;
        }
    }
}
