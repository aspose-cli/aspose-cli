using System.Collections.Concurrent;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Aspose.Cli.TestKit;

/// <summary>
/// The schema ids of the JSON results real CLI runs of this test process produced:
/// <see cref="TempWorkspace"/> records each envelope it has validated against its declared schema.
/// </summary>
public static class ResultSchemaCoverage
{
    private static readonly ConcurrentDictionary<string, byte> Seen = new(StringComparer.Ordinal);

    /// <summary>The ids recorded so far.</summary>
    public static IReadOnlyCollection<string> SeenIds => [.. Seen.Keys];

    internal static void Record(string schemaId) => Seen.TryAdd(schemaId, 0);
}

/// <summary>
/// Every result type a product publishes is produced by at least one real CLI run in that
/// product's tests, so the schema each result type publishes is checked against an engine's real
/// output, and a result type no command produces any more is noticed.
/// </summary>
/// <remarks>
/// The result types are the product's published result schemas (<c>v2/&lt;product&gt;/…</c>
/// except its <c>ops</c> input schema). A run counts when a test's <see cref="TempWorkspace"/>
/// validated its <c>--output json</c> envelope, which <see cref="ResultSchemaCoverage"/> records.
/// The check runs last in its test process (<see cref="ResultSchemaCoverageOrderer"/>) and only
/// where every test of the project runs licensed, as the Full scope does: it is slow and needs a
/// license, so a filtered or unlicensed run, which leaves producing tests out, skips it.
/// </remarks>
public abstract class ResultSchemaCoverageTests
{
    /// <summary>The product id, the second segment of its schema ids.</summary>
    protected abstract string ProductId { get; }

    [Category(TestCategory.Slow)]
    [Fact]
    public void EveryResultSchema_IsProducedByARealRun()
    {
        TestLicense.Require("Licensed tests produce some of the result types.");
        string prefix = $"v2/{ProductId}/";
        string[] published = [.. PublishedSchemas.ResultIds.Where(id => id.StartsWith(prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal)];
        Assert.NotEmpty(published);
        string[] uncovered = [.. published.Except(ResultSchemaCoverage.SeenIds, StringComparer.Ordinal)];

        Assert.True(
            uncovered.Length == 0,
            $"No real --output json run in this test process produced these {ProductId} result schemas: "
            + string.Join(", ", uncovered)
            + ". Add an integration test that runs a command producing each one through TempWorkspace, "
            + "or delete the result type no command produces. Covered: "
            + string.Join(", ", published.Except(uncovered, StringComparer.Ordinal)) + ".");
    }
}

/// <summary>The collection of <see cref="ResultSchemaCoverageTests"/>, which runs after every other collection.</summary>
public static class ResultSchemaCoverageCollection
{
    public const string Name = "Result schema coverage";
}

/// <summary>
/// The default collection order, with the <see cref="ResultSchemaCoverageCollection"/> moved
/// last. xUnit runs collections that disable parallelization after the parallel ones, in this
/// order, so the coverage check sees every run the other tests of its process made.
/// </summary>
public sealed class ResultSchemaCoverageOrderer : ITestCollectionOrderer
{
    public IReadOnlyCollection<TTestCollection> OrderTestCollections<TTestCollection>(
        IReadOnlyCollection<TTestCollection> testCollections)
        where TTestCollection : notnull, ITestCollection
    {
        IReadOnlyCollection<TTestCollection> ordered = DefaultTestCollectionOrderer.Instance.OrderTestCollections(testCollections);
        return
        [
            .. ordered.Where(static collection => !IsCoverage(collection)),
            .. ordered.Where(static collection => IsCoverage(collection)),
        ];
    }

    private static bool IsCoverage(ITestCollection collection) =>
        collection.TestCollectionDisplayName == ResultSchemaCoverageCollection.Name;
}
