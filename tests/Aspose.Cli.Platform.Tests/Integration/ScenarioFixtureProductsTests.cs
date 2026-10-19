using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// The scenario fixtures cover exactly the products of this build, so a test that enumerates
/// <see cref="ScenarioFixtures.Products"/> skips no product and names none the CLI lacks.
/// </summary>
public sealed class ScenarioFixtureProductsTests
{
    [Fact]
    public void Products_AreTheCatalogProducts()
    {
        string[] catalog = [.. CliCatalog.Current.Products.Select(static product => product.Id).Order(StringComparer.Ordinal)];
        string[] fixtures = [.. ScenarioFixtures.Products.Order(StringComparer.Ordinal)];

        Assert.True(catalog.SequenceEqual(fixtures, StringComparer.Ordinal),
            $"ScenarioFixtures lists the products [{string.Join(", ", fixtures)}] but capabilities lists "
            + $"[{string.Join(", ", catalog)}]; give each product a primary format and an operation in ScenarioFixtures.");
    }
}
