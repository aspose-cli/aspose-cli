using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// A homonym exemption of the listed-object vocabulary check covers one property, named by its
/// result schema and JSON pointer: another field of the same name, in the same result schema or
/// another, is still compared, and an exemption that covers no mismatch fails.
/// </summary>
public sealed class OperationVocabularyTests
{
    private const string Item = "#/properties/items/items/properties/index";

    private static readonly JsonObject Operations = Parse("""
        {"type":"object","properties":{"ops":{"type":"array","items":{"$ref":"#/$defs/move"}}},"$defs":{"move":{"type":"object","properties":{"op":{"const":"move"},"index":{"type":"integer"}}}}}
        """);

    private static JsonObject Listing() => Parse("""
        {"type":"object","properties":{"items":{"type":"array","items":{"type":"object","properties":{"index":{"type":"string"}}}}}}
        """);

    [Fact]
    public void AnExemption_CoversItsSchemaAlone()
    {
        string[] mismatches =
        [
            .. OperationVocabulary.Mismatches(
                [Operations],
                [("v2/sample/inspect-result", Listing()), ("v2/sample/read-result", Listing())],
                new Dictionary<(string Schema, string Pointer), string> { [("v2/sample/inspect-result", Item)] = "a listing position" }),
        ];

        Assert.Equal([$"v2/sample/read-result: 'index' at {Item} is string, but operations accept integer"], mismatches);
    }

    [Fact]
    public void AnExemption_CoversOneOfTwoFieldsOfTheSameNameInASchema()
    {
        JsonObject result = Parse("""
            {"type":"object","properties":{
              "sheets":{"type":"array","items":{"$ref":"#/$defs/sheet"}},
              "charts":{"type":"array","items":{"$ref":"#/$defs/chart"}}},
             "$defs":{"sheet":{"type":"object","properties":{"index":{"type":"string"}}},
                      "chart":{"type":"object","properties":{"index":{"type":"boolean"}}}}}
            """);

        string[] mismatches =
        [
            .. OperationVocabulary.Mismatches(
                [Operations],
                [("v2/sample/inspect-result", result)],
                new Dictionary<(string Schema, string Pointer), string> { [("v2/sample/inspect-result", "#/$defs/sheet/properties/index")] = "a sheet's tab label" }),
        ];

        Assert.Equal(["v2/sample/inspect-result: 'index' at #/$defs/chart/properties/index is boolean, but operations accept integer"], mismatches);
    }

    [Fact]
    public void AnExemptionThatCoversNothing_Fails()
    {
        string[] mismatches =
        [
            .. OperationVocabulary.Mismatches(
                [Operations],
                [("v2/sample/inspect-result", Listing())],
                new Dictionary<(string Schema, string Pointer), string>
                {
                    [("v2/sample/inspect-result", Item)] = "a listing position",
                    [("v2/sample/read-result", Item)] = "no such schema",
                }),
        ];

        Assert.Equal(
            [$"v2/sample/read-result: {Item} is exempted as a homonym, but it matches the operations or is no listed field; delete the exemption"],
            mismatches);
    }

    private static JsonObject Parse(string json) => JsonNode.Parse(json)!.AsObject();
}
