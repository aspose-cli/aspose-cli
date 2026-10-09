using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Operations;
using Json.Schema;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

/// <summary>
/// One declaration drives parsing, defaults, validation and the published schema, shown on the
/// test vocabulary, which the operation and JSON source generators compile as a product's.
/// </summary>
public sealed class OperationContractTests
{
    private static readonly Lazy<JsonSchema> Published = new(static () => JsonSchema.FromText(TestOp.Catalog.Describe("edit").Schema.Document));

    public static TheoryData<string, OperationValueKind, string, string> Constraints => new()
    {
        { nameof(MinLengthAttribute), OperationValueKind.String, "\"a\"", "\"\"" },
        { nameof(MaxLengthAttribute), OperationValueKind.String, "\"😀😀\"", "\"abc\"" },
        { nameof(MinimumAttribute), OperationValueKind.Integer, "1", "0" },
        { nameof(MaximumAttribute), OperationValueKind.Number, "1", "1.5" },
        { nameof(ExclusiveMinimumAttribute), OperationValueKind.Number, "0.5", "0" },
        { nameof(MinItemsAttribute), OperationValueKind.Array, "[\"a\"]", "[]" },
        { nameof(MaxItemsAttribute), OperationValueKind.Array, "[\"a\"]", "[\"a\",\"b\"]" },
        { nameof(PatternAttribute), OperationValueKind.String, "\"aa\"", "\"aa\\n\"" },
        { nameof(AllowedValuesAttribute), OperationValueKind.Integer, "180", "91" },
        { nameof(PageRangeAttribute), OperationValueKind.String, "\" 1-3, 7,9- \"", "\"0\"" },
        { nameof(HexColorAttribute), OperationValueKind.String, "\"#A0b1C2\"", "\"#12345\"" },
        { nameof(WebLinkAttribute), OperationValueKind.String, "\"mailto:team@example.test\"", "\"javascript:alert(1)\"" },
        { nameof(SecretEnvAttribute), OperationValueKind.String, "\" A \"", "\" \"" },
        { nameof(JsonScalarAttribute), OperationValueKind.Any, "5", "{}" },
    };

    /// <summary>
    /// A pattern keeps its ECMA-262 meaning where .NET's ECMAScript mode differs, which the
    /// schema library used above cannot show: Unicode white space and line terminators.
    /// </summary>
    [Theory]
    [InlineData(@"\S", "\u00A0\u3000", false)]
    [InlineData(@"^\s+$", "\u00A0\u2028", true)]
    [InlineData(@"^[\s,]+$", "\u00A0,", true)]
    [InlineData("^.$", "\u2028", false)]
    [InlineData("^a.$", "a\u00E9", true)]
    public void Pattern_UsesEcma262WhiteSpaceAndLineTerminators(string pattern, string value, bool matches) =>
        Assert.Equal(matches, new PatternAttribute(pattern).Check(value) is null);

    /// <summary>A pattern with a meaning explains a rejection in its own words; the schema still states only the pattern.</summary>
    [Fact]
    public void Pattern_StatesItsMeaningWhenItRejects()
    {
        var plain = new PatternAttribute("^a+$");
        var explained = new PatternAttribute("^a+$") { Meaning = "must be a run of a's such as \"aa\"" };

        Assert.Equal("must match the pattern ^a+$", plain.Check("b"));
        Assert.Equal("must be a run of a's such as \"aa\" (pattern ^a+$)", explained.Check("b"));
        Assert.Null(explained.Check("aa"));

        var schema = new JsonObject();
        explained.Describe(schema);
        Assert.Equal("^a+$", Assert.Single(schema).Value!.GetValue<string>());
        Assert.True(schema.ContainsKey("pattern"));
    }

    /// <summary>Each constraint accepts and rejects exactly what the schema it writes accepts and rejects.</summary>
    [Theory]
    [MemberData(nameof(Constraints))]
    public void Constraint_ChecksWhatItsSchemaStates(string name, OperationValueKind kind, string valid, string invalid)
    {
        ValueConstraintAttribute constraint = name switch
        {
            nameof(MinLengthAttribute) => new MinLengthAttribute(1),
            nameof(MaxLengthAttribute) => new MaxLengthAttribute(2),
            nameof(MinimumAttribute) => new MinimumAttribute(1),
            nameof(MaximumAttribute) => new MaximumAttribute(1),
            nameof(ExclusiveMinimumAttribute) => new ExclusiveMinimumAttribute(0),
            nameof(MinItemsAttribute) => new MinItemsAttribute(1),
            nameof(MaxItemsAttribute) => new MaxItemsAttribute(1),
            nameof(PatternAttribute) => new PatternAttribute("^a+$"),
            nameof(AllowedValuesAttribute) => new AllowedValuesAttribute(typeof(Angles)),
            nameof(PageRangeAttribute) => new PageRangeAttribute(),
            nameof(HexColorAttribute) => new HexColorAttribute(),
            nameof(JsonScalarAttribute) => new JsonScalarAttribute(),
            nameof(SecretEnvAttribute) => new SecretEnvAttribute(),
            _ => new WebLinkAttribute(),
        };
        var schema = new JsonObject();
        if (kind != OperationValueKind.Any)
        {
            schema["type"] = kind.ToString().ToLowerInvariant();
        }

        constraint.Describe(schema);
        JsonSchema published = JsonSchema.FromText(schema.ToJsonString());

        foreach ((string value, bool accepted) in new[] { (valid, true), (invalid, false) })
        {
            using JsonDocument document = JsonDocument.Parse(value);
            // JsonSchema.Net evaluates patterns with .NET semantics, whose $ also matches before a
            // final line break; ECMA-262, which JSON Schema names and the check follows, does not.
            if (!value.Contains("\\n", StringComparison.Ordinal))
            {
                Assert.Equal(accepted, published.Evaluate(document.RootElement).IsValid);
            }

            Assert.Equal(accepted, constraint.Check(Clr(document.RootElement, kind)) is null);
        }
    }

    [Fact]
    public void Parse_FillsOmittedMembersFromTheirPublishedDefaults()
    {
        JsonObject place = JsonNode.Parse(Parse("""{"op":"place","pages":"2","all":true}"""))!.AsObject();

        // The box takes its {} default, and the box's own width default fills that object.
        Assert.Equal("""{"width":1}""", place["box"]!.ToJsonString());
        Assert.Equal("light", place["shade"]!.GetValue<string>());
        JsonObject definitions = Schema()["$defs"]!.AsObject();
        Assert.Equal("{}", definitions["place"]!["properties"]!["box"]!["default"]!.ToJsonString());
        Assert.Equal("1", definitions["box"]!["properties"]!["width"]!["default"]!.ToJsonString());
    }

    [Theory]
    [InlineData("""{"op":"place","pages":"1","all":true,"box":{"width":0}}""", "box.width must be greater than 0")]
    [InlineData("""{"op":"place","pages":"1","all":true,"colors":["#12"]}""", "colors[0] must be a #RRGGBB color")]
    [InlineData("""{"op":"place","pages":"1","all":true,"colors":[]}""", "colors must not be empty")]
    [InlineData("""{"op":"place","pages":"1","all":true,"rows":[["a"],[]]}""", "rows[1] must not be empty")]
    [InlineData("""{"op":"place","pages":"1","all":true,"rows":[["a","b","c"]]}""", "rows[0] must contain at most 2 items")]
    [InlineData("""{"op":"place","pages":"1","all":true,"rows":[["a"],["b"],["c"],["d"]]}""", "rows must contain at most 3 items")]
    [InlineData("""{"op":"place","pages":"1","all":true,"style":{}}""", "style must set at least one member")]
    [InlineData("""{"op":"place","pages":"1","all":true,"style":{"size":3}}""", "style must set font when it sets size")]
    [InlineData("""{"op":"place","pages":"1","all":true,"cells":[1,null,{}]}""", "cells[2] must be a string, number, Boolean or null")]
    [InlineData("""{"op":"place","pages":"1","all":true,"tags":["draft","Final"]}""", "tags[1] must be a lowercase tag such as \"draft\" (pattern ^[a-z]+$)")]
    [InlineData("""{"op":"place","pages":"1","all":true,"labels":{"a":"x","a":"y"}}""", "labels.a is duplicated")]
    [InlineData("""{"op":"place","pages":"1","all":true,"labels":{"a":null}}""", "labels.a must not be null")]
    [InlineData("""{"op":"place","pages":"1","all":true,"shade":"grey"}""", "shade must be one of: light, dark")]
    [InlineData("""{"op":"place","pages":"1"}""", "the operation must set exactly one of: path, all")]
    [InlineData("""{"op":"place","pages":"1","all":true,"path":"a"}""", "the operation must set exactly one of: path, all")]
    [InlineData("""{"op":"place","pages":"1","path":null}""", "path must not be null")]
    [InlineData("""{"op":"note","text":""}""", "text must not be empty")]
    [InlineData("""{"op":"note"}""", "the operation must set at least one of: text, pinned")]
    [InlineData("""{"op":"place","pages":"0","all":true}""", "pages must be a 1-based page range such as 1-3,7,9-: '0' is not a positive 1-based number")]
    [InlineData("""{"op":"link","path":" "}""", "path must not be blank")]
    [InlineData("""{"op":"shift"}""", "the operation must set to when mode is absolute")]
    [InlineData("""{"op":"shift","mode":"relative"}""", "the operation must set by when mode is relative")]
    [InlineData("""{"op":"shift","to":1,"by":2}""", "the operation must not set by unless mode is relative")]
    [InlineData("""{"op":"set"}""", "the required field 'value' is missing")]
    [InlineData("""{"op":"paint"}""", "the required field 'shades' is missing; it takes: light, dark")]
    public void Parse_RejectsABrokenRuleByItsWirePath(string operation, string reason) =>
        Assert.Equal("error: " + reason, Parse(operation));

    [Fact]
    public void Parse_CountsAMemberAsItsSchemaDoesAndRunsTheOperationsOwnRules()
    {
        JsonObject place = JsonNode.Parse(Parse("""{"op":"place","pages":" 1-3 ,5","all":false,"path":"a"}"""))!.AsObject();

        // A Boolean that is not nullable is set only when true; a nullable one whenever present.
        Assert.Equal("1-3,5", place["pages"]!.GetValue<string>());
        Assert.False(JsonNode.Parse(Parse("""{"op":"note","pinned":false}"""))!["pinned"]!.GetValue<bool>());
    }

    /// <summary>The schema's conditional of a present-when rule accepts exactly what the parser accepts, defaults included.</summary>
    [Theory]
    [InlineData("""{"op":"shift","to":1}""", true)]
    [InlineData("""{"op":"shift","mode":"relative","by":2}""", true)]
    [InlineData("""{"op":"shift","mode":"absolute","to":1}""", true)]
    [InlineData("""{"op":"shift"}""", false)]
    [InlineData("""{"op":"shift","mode":"relative"}""", false)]
    [InlineData("""{"op":"shift","mode":"relative","by":2,"to":1}""", false)]
    [InlineData("""{"op":"shift","to":1,"by":2}""", false)]
    public void PresentWhen_ParserAndSchemaAgree(string operation, bool valid)
    {
        using JsonDocument document = JsonDocument.Parse($$"""{"ops":[{{operation}}]}""");

        Assert.Equal(valid, !Parse(operation).StartsWith("error: ", StringComparison.Ordinal));
        Assert.Equal(valid, Published.Value.Evaluate(document.RootElement).IsValid);
    }

    /// <summary>A required member whose type admits null must be present and may be null; an optional one is omitted, never null.</summary>
    [Theory]
    [InlineData("""{"op":"label","text":null}""", true)]
    [InlineData("""{"op":"label","text":"a"}""", true)]
    [InlineData("""{"op":"label","text":""}""", false)]
    [InlineData("""{"op":"label"}""", false)]
    [InlineData("""{"op":"note","text":null}""", false)]
    public void NullableRequiredMember_ParserAndSchemaAgree(string operation, bool valid)
    {
        using JsonDocument document = JsonDocument.Parse($$"""{"ops":[{{operation}}]}""");

        Assert.Equal(valid, !Parse(operation).StartsWith("error: ", StringComparison.Ordinal));
        Assert.Equal(valid, Published.Value.Evaluate(document.RootElement).IsValid);
        Assert.Equal("""["string","null"]""", Schema()["$defs"]!["label"]!["properties"]!["text"]!["type"]!.ToJsonString());
    }

    [Fact]
    public void Schema_IsDeterministicAndStatesEveryDeclaredRule()
    {
        GeneratedOperationSchema generated = TestOp.Catalog.Describe("edit").Schema;
        JsonObject place = Schema()["$defs"]!["place"]!.AsObject();

        Assert.Equal(generated.Document, TestOp.Catalog.Describe("edit").Schema.Document);
        Assert.EndsWith("}\n", generated.Document, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', generated.Document);
        Assert.Equal("""["op","pages"]""", place["required"]!.ToJsonString());
        Assert.Equal("#/$defs/pages", place["properties"]!["pages"]!["$ref"]!.GetValue<string>());
        Assert.Equal("#/$defs/color", place["properties"]!["colors"]!["items"]!["$ref"]!.GetValue<string>());
        Assert.Equal("""{"type":"array","maxItems":3,"items":{"type":"array","minItems":1,"maxItems":2,"items":{"type":"string"}}}""", place["properties"]!["rows"]!.ToJsonString());
        Assert.Equal("""["string","number","boolean","null"]""", place["properties"]!["cells"]!["items"]!["type"]!.ToJsonString());
        Assert.Equal("""["light","dark"]""", place["properties"]!["shade"]!["enum"]!.ToJsonString());
        Assert.Equal(0, place["properties"]!["count"]!["minimum"]!.GetValue<int>());
        Assert.Equal(
            """[{"oneOf":[{"required":["path"]},{"required":["all"],"properties":{"all":{"const":true}}}]}]""",
            place["allOf"]!.ToJsonString());
        Assert.Equal("""[{"anyOf":[{"required":["text"]},{"required":["pinned"]}]}]""", Schema()["$defs"]!["note"]!["allOf"]!.ToJsonString());
        Assert.Equal("""[{"minProperties":1},{"dependentRequired":{"size":["font"]}}]""", Schema()["$defs"]!["style"]!["allOf"]!.ToJsonString());
        // The default of mode meets the second condition, so only the first requires mode.
        Assert.Equal(
            """[{"if":{"properties":{"mode":{"const":"relative"}},"required":["mode"]},"then":{"required":["by"]},"else":{"not":{"required":["by"]}}},"""
            + """{"if":{"properties":{"mode":{"const":"absolute"}}},"then":{"required":["to"]},"else":{"not":{"required":["to"]}}}]""",
            Schema()["$defs"]!["shift"]!["allOf"]!.ToJsonString());
        Assert.Equal("""{"type":"string","minLength":1,"pattern":"\\S"}""", Schema()["$defs"]!["link"]!["properties"]!["path"]!.ToJsonString());
        string description = Schema()["description"]!.GetValue<string>();
        Assert.StartsWith("Test operations. Operations apply in order. Without --best-effort the batch is atomic", description, StringComparison.Ordinal);
        Assert.EndsWith("still writes nothing.", description, StringComparison.Ordinal);
    }

    [Fact]
    public void OperationView_KeepsOnlyTheDefinitionsTheOperationReaches()
    {
        JsonObject view = JsonNode.Parse(TestOp.Catalog.Describe("edit").Schema.Operations["note"])!.AsObject();

        Assert.Equal(["id", "note"], view["$defs"]!.AsObject().Select(static entry => entry.Key));
        Assert.Equal("#/$defs/note", view["properties"]!["ops"]!["items"]!["oneOf"]![0]!["$ref"]!.GetValue<string>());
    }

    private static class Angles
    {
        public const int Quarter = 90;
        public const int Half = 180;
        public const int ThreeQuarters = 270;
    }

    private static string Parse(string operation)
    {
        try
        {
            TestBatch batch = TestOp.Catalog.Parse<TestBatch>($$"""{"ops":[{{operation}}]}""", TestContracts.Json);
            return JsonSerializer.Serialize(batch.Ops[0], TestContracts.Json.LocalOptions);
        }
        catch (CliException error)
        {
            return "error: " + error.Details!["reason"]!.GetValue<string>();
        }
    }

    private static JsonObject Schema() => JsonNode.Parse(TestOp.Catalog.Describe("edit").Schema.Document)!.AsObject();

    private static object Clr(JsonElement value, OperationValueKind kind) => kind == OperationValueKind.Any
        ? value
        : value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()!,
            JsonValueKind.Number => value.TryGetInt32(out int whole) ? whole : value.GetDouble(),
            JsonValueKind.Array => value.EnumerateArray().Select(item => Clr(item, kind)).ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };
}
