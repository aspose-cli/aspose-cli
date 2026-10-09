using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// The contract snapshots are split by owner, so a change to one product touches one product's
/// file: <c>capabilities/&lt;product&gt;.json</c> for each product (with its operations and result
/// schemas), <c>capabilities/host.json</c> for the host commands, a summary, and no single
/// capabilities snapshot of every product.
/// </summary>
public sealed class ContractSnapshotLayoutTests
{
    private static readonly string Snapshots =
        Path.Combine(RepositoryPaths.Root, "tests", "Aspose.Cli.Platform.Tests", "Integration", "Snapshots");

    [Fact]
    public void CapabilitiesSnapshots_AreSplitByProduct()
    {
        string[] expected =
        [
            .. CliCatalog.Current.Products.Select(static product => Path.Combine("capabilities", product.Id + ".json")),
            Path.Combine("capabilities", "host.json"),
        ];
        string[] missing = [.. expected.Where(static file => !File.Exists(Path.Combine(Snapshots, file)))];
        bool summary = Directory.EnumerateFiles(Snapshots, "*summary*.json", SearchOption.AllDirectories).Any();
        bool whole = File.Exists(Path.Combine(Snapshots, "capabilities.json"));

        Assert.True(missing.Length == 0 && summary && !whole,
            $"The capabilities snapshots under {Path.GetRelativePath(RepositoryPaths.Root, Snapshots)} are one file per product plus the host and a summary. "
            + $"Missing: [{string.Join(", ", missing)}]{(summary ? string.Empty : "; no summary snapshot")}"
            + (whole ? "; capabilities.json still holds every product in one file" : string.Empty) + ".");
    }
}
