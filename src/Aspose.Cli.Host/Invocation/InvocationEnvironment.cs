using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Host.Invocation;

/// <summary>The environment variables a command reads.</summary>
internal static class InvocationEnvironment
{
    /// <summary>The environment variables a license is resolved from: the shared ones and each product's.</summary>
    internal static IReadOnlyList<string> LicenseVariables(ProductCatalog catalog) =>
        new[] { LicenseResolver.EnvPathName, LicenseResolver.EnvBase64Name }
            .Concat(catalog.Products.SelectMany(static product => new[]
            {
                LicenseResolver.ProductEnvPathName(product.Manifest.Id),
                LicenseResolver.ProductEnvBase64Name(product.Manifest.Id),
            }))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    internal static Func<string, string?> CreateSecretReader(ResourceBudgetLedger budgets) =>
        new EnvironmentSecrets(Environment.GetEnvironmentVariable, budgets).Read;
}
