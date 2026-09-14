using System.Diagnostics;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Host.Invocation;

/// <summary>Explicit child-process environment policy shared by MCP and supervised commands.</summary>
internal static class InvocationEnvironment
{
    private static readonly string[] Baseline =
    [
        "SystemRoot", "WINDIR", "TEMP", "TMP", "PATH", "PATHEXT",
        "USERPROFILE", "HOMEDRIVE", "HOMEPATH", "HOME",
        "LOCALAPPDATA", "APPDATA", "PROGRAMDATA", "LANG", "LC_ALL", "LC_CTYPE",
        Aspose.Cli.Sdk.Configuration.ConfigurationPaths.EnvironmentVariableName, LicenseResolver.EnvPathName, LicenseResolver.EnvBase64Name,
    ];

    internal static IReadOnlyList<string> ProductVariables(ProductCatalog catalog) =>
        catalog.Products.SelectMany(static product => new[]
            {
                LicenseResolver.ProductEnvPathName(product.Manifest.Id),
                LicenseResolver.ProductEnvBase64Name(product.Manifest.Id),
            }.Concat(product.Manifest.ResourceBudgets
                .Select(static budget => budget.EnvironmentVariable)
                .OfType<string>()))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    internal static Func<string, string?> CreateSecretReader(ResourceBudgetLedger budgets)
    {
        InvocationInputs? inputs = InvocationInputs.Current;
        return new EnvironmentSecrets(name =>
        {
            if (inputs is null) { return Environment.GetEnvironmentVariable(name); }
            EnvironmentValueReply reply = inputs.ReadEnvironment(name,
                budgets.Limit(ResourceBudgetKinds.SecretCharacters), budgets.Deadline.Token,
                bytes => budgets.Consume(ResourceBudgetKinds.MemoryBufferBytes, bytes, "bytes", "process-input"));
            if (reply.OversizedCharacters is { } oversized)
            {
                budgets.Consume(ResourceBudgetKinds.SecretCharacters, oversized, "characters", "environment-secret");
                throw new InvalidDataException("The environment reply has an invalid size.");
            }
            return reply.Value;
        }, budgets).Read;
    }
    internal static void Configure(
        ProcessStartInfo start, ParsedInvocation invocation, IReadOnlyList<string> productVariables)
    {
        start.Environment.Clear();
        foreach (string name in Baseline.Concat(productVariables))
        {
            if (Environment.GetEnvironmentVariable(name) is { } value)
            {
                start.Environment[name] = value;
            }
        }
        if (invocation.GlobalValues is { } globals)
        {
            start.Environment[InputSizeGuard.BudgetVariable] =
                globals.MaxInputBytes.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

}
