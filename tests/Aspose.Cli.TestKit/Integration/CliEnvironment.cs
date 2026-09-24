using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Configuration;

namespace Aspose.Cli.TestKit;

/// <summary>
/// The isolated evaluation environment a CLI test executes under. It replaces
/// the user config directory and strips every license source. Extra
/// <c>variables</c> let a test drive env-configured behavior (password-env,
/// size budgets, ...).
/// </summary>
public sealed class CliEnvironment
{
    private static readonly IReadOnlyDictionary<string, string?> NoVariables =
        new Dictionary<string, string?>();

    private readonly string _configRoot;
    private readonly IReadOnlyDictionary<string, string?> _variables;

    private CliEnvironment(string configRoot, IReadOnlyDictionary<string, string?>? variables)
    {
        _configRoot = configRoot;
        _variables = variables ?? NoVariables;
    }

    /// <summary>An evaluation-mode run: every license source is stripped.</summary>
    public static CliEnvironment Evaluation(
        string configRoot, IReadOnlyDictionary<string, string?>? variables = null) =>
        new(configRoot, variables);

    /// <summary>
    /// Drops inherited CLI settings, then applies the extra variables, the config isolation
    /// and the license policy.
    /// </summary>
    public void Apply(IDictionary<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        Remove(environment, TestEnvironment.IsIsolated);
        foreach ((string key, string? value) in _variables)
        {
            environment[key] = value;
        }
        environment["APPDATA"] = _configRoot;
        environment["XDG_CONFIG_HOME"] = _configRoot;
        environment[ConfigurationPaths.EnvironmentVariableName] = Path.Combine(_configRoot, DistributionInfo.ConfigurationDirectoryName);
        Remove(environment, IsLicenseSource);
    }

    private static bool IsLicenseSource(string key) =>
        key.StartsWith("ASPOSE_", StringComparison.OrdinalIgnoreCase)
        && (key.EndsWith("_LICENSE_B64", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith("_LICENSE_PATH", StringComparison.OrdinalIgnoreCase));

    private static void Remove(IDictionary<string, string?> environment, Func<string, bool> selected)
    {
        foreach (string key in environment.Keys.Where(selected).ToArray())
        {
            environment.Remove(key);
        }
    }
}
