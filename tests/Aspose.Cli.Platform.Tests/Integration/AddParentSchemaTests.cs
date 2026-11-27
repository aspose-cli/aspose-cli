using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// Every add names its parent in <c>in</c>, the document itself included, so each add of a
/// product's ops schema takes <c>in</c> and requires it, whichever kinds of parent it adds to.
/// </summary>
public sealed class AddParentSchemaTests
{
    [Fact]
    public void EveryAdd_RequiresIn()
    {
        var adds = new List<string>();
        var without = new List<string>();
        foreach (string id in PublishedSchemas.Ids.Where(static id => id.EndsWith("/ops", StringComparison.Ordinal)))
        {
            foreach ((string name, JsonNode? definition) in PublishedSchemas.Document(id)["$defs"]!.AsObject().Where(static entry => entry.Key.StartsWith("add.", StringComparison.Ordinal)))
            {
                adds.Add($"{id} {name}");
                bool required = definition!["required"]!.AsArray().Any(static field => field!.GetValue<string>() == "in");
                if (definition["properties"]!["in"] is null || !required)
                {
                    without.Add($"{id} {name}");
                }
            }
        }

        Assert.NotEmpty(adds);
        Assert.True(without.Count == 0, $"{without.Count} of {adds.Count} adds do not require in:{Environment.NewLine}{string.Join(Environment.NewLine, without)}");
    }

    /// <summary>A PDF bookmark adds to the document or to a bookmark; the schema takes either, and no add without in.</summary>
    [Theory]
    [InlineData("""{"ops":[{"add":"bookmark","in":"document","args":{"title":"One"}}]}""", true)]
    [InlineData("""{"ops":[{"add":"bookmark","in":"bookmark/1","args":{"title":"One.a"}}]}""", true)]
    [InlineData("""{"ops":[{"add":"bookmark","args":{"title":"One"}}]}""", false)]
    [InlineData("""{"ops":[{"add":"page"}]}""", false)]
    public void APdfAdd_IsValidWithTheAddressOfEitherParentInIn(string ops, bool valid)
    {
        using JsonDocument instance = JsonDocument.Parse(ops);

        Assert.Equal(valid, PublishedSchemas.Schema("v3/pdf/ops").Evaluate(instance.RootElement).IsValid);
    }
}
