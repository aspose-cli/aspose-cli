using System.Runtime.CompilerServices;
using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Tests;

/// <summary>Validates diagnostics contributed by the complete Commercial catalog.</summary>
public sealed class CommercialDiagnosticCatalogTests
{
    [Fact]
    public void FullCatalog_AggregatesCommonAndEveryCompiledProductOwner()
    {
        ProductCatalog catalog = CompiledProductCatalog.Instance;
        string[] active = catalog.Products
            .Select(static product => product.Manifest.Id)
            .ToArray();

        Assert.NotEmpty(catalog.Diagnostics.Errors);
        Assert.NotEmpty(catalog.Diagnostics.Warnings);
        Assert.All(catalog.Diagnostics.All, descriptor =>
            Assert.True(
                descriptor.Owner == CommonDiagnostics.Owner
                || active.Contains(descriptor.Owner, StringComparer.Ordinal),
                $"{descriptor.Code}: {descriptor.Owner}"));
        Assert.All(catalog.Products, product =>
            Assert.NotNull(product.Diagnostics));
    }

    [Fact]
    public void SdkDiagnosticSources_ContainNoProductOwnedCodes()
    {
        ProductCatalog catalog = CompiledProductCatalog.Instance;
        string sdkDiagnostics = string.Join(
            Environment.NewLine,
            File.ReadAllText(RepositoryFile(
                "src/Aspose.Cli.Sdk/Errors",
                "ErrorCodes.cs")),
            File.ReadAllText(RepositoryFile(
                "src/Aspose.Cli.Sdk/Errors",
                "CliErrors.cs")),
            File.ReadAllText(RepositoryFile(
                "src/Aspose.Cli.Sdk/Contracts",
                "Warning.cs")));

        HashSet<string> commonCodes = CommonDiagnostics.All
            .Select(static item => item.Code)
            .ToHashSet(StringComparer.Ordinal);
        foreach (DiagnosticDescriptor descriptor in catalog.Diagnostics.All
            .Where(item =>
                item.Owner != CommonDiagnostics.Owner
                && !commonCodes.Contains(item.Code)))
        {
            Assert.DoesNotContain(
                $"\"{descriptor.Code}\"",
                sdkDiagnostics,
                StringComparison.Ordinal);
        }
    }

    private static string RepositoryFile(
        string directory,
        string name,
        [CallerFilePath] string source = "")
    {
        for (DirectoryInfo? current = new FileInfo(source).Directory;
            current is not null;
            current = current.Parent)
        {
            string candidate = Path.Combine(current.FullName, directory, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
        throw new FileNotFoundException(name);
    }
}
