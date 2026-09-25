using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Architecture.Tests;
using Aspose.Cli.Sdk.Analyzers;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Operations;
using Json.Schema;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

/// <summary>
/// One declaration drives parsing, defaults, validation and the published schema. The sample
/// vocabulary is compiled here through the operation generator and the JSON source generator,
/// exactly as a product compiles its own.
/// </summary>
public sealed class OperationContractTests
{
    private const string Sample =
        """
        using System.Collections.Generic;
        using System.Text.Json;
        using System.Text.Json.Serialization;
        using Aspose.Cli.Sdk.Addressing;
        using Aspose.Cli.Sdk.Contracts;
        using Aspose.Cli.Sdk.Errors;
        using Aspose.Cli.Sdk.Operations;
        using Aspose.Cli.Sdk.Serialization;
        namespace Sample;

        public static class Shades { public const string Light = "light"; public const string Dark = "dark"; }

        /// <summary>Test operations.</summary>
        [OperationVocabulary("https://schemas.aspose.dev/aspose-cli/v2/test/ops.schema.json", MaximumOperations = 8, JsonContext = typeof(SampleJsonContext))]
        [JsonConverter(typeof(OperationJsonConverter<SampleOp>))]
        public abstract partial record SampleOp : BoundedOperation;

        [ExactlyOneOf("path", "all")]
        public abstract record TargetOp : SampleOp
        {
            [MinLength(1)] public string? Path { get; init; }
            public bool All { get; init; }
        }

        public sealed record Box { [ExclusiveMinimum(0)] public double Width { get; init; } = 1; }

        [MinProperties(1), DependentRequired("size", "font")]
        public sealed record Style { public string? Font { get; init; } public double? Size { get; init; } public bool? Bold { get; init; } }

        /// <summary>Places boxes.</summary>
        [Operation("place")]
        public sealed record PlaceOp : TargetOp
        {
            [PageRange] public required string Pages { get; init; }
            public Box Box { get; init; } = new();
            public Style? Style { get; init; }
            [MinItems(1), HexColor] public IReadOnlyList<string>? Colors { get; init; }
            [MaxItems(3), MinItems(1, Depth = 1), MaxItems(2, Depth = 1)] public IReadOnlyList<IReadOnlyList<string>>? Rows { get; init; }
            [JsonScalar] public IReadOnlyList<object?>? Cells { get; init; }
            public IReadOnlyDictionary<string, string>? Labels { get; init; }
            [AllowedValues(typeof(Shades))] public string Shade { get; init; } = Shades.Light;
            public uint Count { get; init; }

            protected override BoundedOperation Validated() => this with { Pages = PageRange.Parse(Pages).Text };
        }

        [Operation("note")]
        [AtLeastOneOf("text", "pinned")]
        public sealed record NoteOp : SampleOp { [MinLength(1)] public string? Text { get; init; } public bool? Pinned { get; init; } }

        public sealed record SampleBatch : BoundedOperationEnvelope<SampleOp>;

        [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonSerializable(typeof(SampleBatch))]
        [JsonSerializable(typeof(PlaceOp))]
        [JsonSerializable(typeof(NoteOp))]
        public partial class SampleJsonContext : JsonSerializerContext;

        public static class Probe
        {
            private static readonly ProductJsonDefinition Contracts = new("test", SampleJsonContext.Default);

            public static string Parse(string json)
            {
                try
                {
                    return JsonSerializer.Serialize(SampleOp.Catalog.Parse<SampleBatch>(json, Contracts).Ops[0], Contracts.LocalOptions);
                }
                catch (CliException error)
                {
                    return "error: " + error.Details!["reason"]!.GetValue<string>();
                }
            }

            public static object Describe() => SampleOp.Catalog.Describe("edit");
        }
        """;

    private static readonly Lazy<Type> Probe = new(Compile);

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
            _ => new WebLinkAttribute(),
        };
        var schema = new JsonObject();
        if (kind != OperationValueKind.Any)
        {
            schema["type"] = kind.ToString().ToLowerInvariant();
        }

        constraint.Describe(schema, new OperationValue { Kind = kind });
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
    [InlineData("""{"op":"place","pages":"1","all":true,"labels":{"a":"x","a":"y"}}""", "labels.a is duplicated")]
    [InlineData("""{"op":"place","pages":"1","all":true,"labels":{"a":null}}""", "labels.a must not be null")]
    [InlineData("""{"op":"place","pages":"1","all":true,"shade":"grey"}""", "shade must be one of: light, dark")]
    [InlineData("""{"op":"place","pages":"1"}""", "the operation must set exactly one of: path, all")]
    [InlineData("""{"op":"place","pages":"1","all":true,"path":"a"}""", "the operation must set exactly one of: path, all")]
    [InlineData("""{"op":"place","pages":"1","path":null}""", "path must not be null")]
    [InlineData("""{"op":"note","text":""}""", "text must not be empty")]
    [InlineData("""{"op":"note"}""", "the operation must set at least one of: text, pinned")]
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

    [Fact]
    public void Schema_IsDeterministicAndStatesEveryDeclaredRule()
    {
        GeneratedOperationSchema generated = Command().GeneratedSchema!;
        JsonObject place = Schema()["$defs"]!["place"]!.AsObject();

        Assert.Equal(generated.Document, Command().GeneratedSchema!.Document);
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
    }

    [Fact]
    public void OperationView_KeepsOnlyTheDefinitionsTheOperationReaches()
    {
        JsonObject view = JsonNode.Parse(Command().GeneratedSchema!.Operations["note"])!.AsObject();

        Assert.Equal(["id", "note"], view["$defs"]!.AsObject().Select(static entry => entry.Key));
        Assert.Equal("#/$defs/note", view["properties"]!["ops"]!["items"]!["oneOf"]![0]!["$ref"]!.GetValue<string>());
    }

    private static class Angles
    {
        public const int Quarter = 90;
        public const int Half = 180;
        public const int ThreeQuarters = 270;
    }

    private static string Parse(string operation) =>
        (string)Probe.Value.GetMethod("Parse")!.Invoke(null, [$$"""{"ops":[{{operation}}]}"""])!;

    private static ProductOperationCommand Command() =>
        (ProductOperationCommand)Probe.Value.GetMethod("Describe")!.Invoke(null, null)!;

    private static JsonObject Schema() => JsonNode.Parse(Command().GeneratedSchema!.Document)!.AsObject();

    private static object Clr(JsonElement value, OperationValueKind kind) => kind == OperationValueKind.Any
        ? value
        : value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()!,
            JsonValueKind.Number => value.TryGetInt32(out int whole) ? whole : value.GetDouble(),
            JsonValueKind.Array => value.EnumerateArray().Select(item => Clr(item, kind)).ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

    /// <summary>Compiles the sample with the operation and JSON source generators and loads it.</summary>
    private static Type Compile()
    {
        string runtime = RuntimeEnvironment.GetRuntimeDirectory();
        string packs = Path.GetFullPath(Path.Combine(runtime, "..", "..", "..", "packs", "Microsoft.NETCore.App.Ref"));
        string jsonGenerator = Directory.GetDirectories(packs, $"{Environment.Version.Major}.*")
            .OrderBy(static directory => Version.Parse(Path.GetFileName(directory)))
            .Select(static directory => Path.Combine(directory, "analyzers", "dotnet", "cs", "System.Text.Json.SourceGeneration.dll"))
            .Last(File.Exists);
        CSharpCompilation compilation = CSharpCompilation.Create(
            "Sample.Vocabulary",
            [CSharpSyntaxTree.ParseText(Sample, new CSharpParseOptions(LanguageVersion.Preview))],
            RoslynTestSupport.PlatformReferences().Append(MetadataReference.CreateFromFile(typeof(OperationCatalog<>).Assembly.Location)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        ISourceGenerator[] generators =
        [
            new OperationContractGenerator().AsSourceGenerator(),
            .. new AnalyzerFileReference(jsonGenerator, new AssemblyLoader()).GetGenerators(LanguageNames.CSharp),
        ];
        CSharpGeneratorDriver.Create(generators, parseOptions: new CSharpParseOptions(LanguageVersion.Preview))
            .RunGeneratorsAndUpdateCompilation(compilation, out Compilation generated, out ImmutableArray<Diagnostic> _);
        using var image = new MemoryStream();
        Microsoft.CodeAnalysis.Emit.EmitResult emitted = generated.Emit(image);
        Assert.True(emitted.Success, string.Join("\n", emitted.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        return Assembly.Load(image.ToArray()).GetType("Sample.Probe")!;
    }

    private sealed class AssemblyLoader : IAnalyzerAssemblyLoader
    {
        public void AddDependencyLocation(string fullPath)
        {
        }

        public Assembly LoadFromPath(string fullPath) => Assembly.LoadFrom(fullPath);
    }
}
