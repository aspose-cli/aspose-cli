using Aspose.Cli.Sdk.Analyzers;
using Aspose.Cli.Sdk.Operations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>The operation generator extracts records into descriptors and rejects incomplete contracts.</summary>
public sealed class OperationContractGeneratorTests
{
    private const string Prelude =
        """
        using System.Collections.Generic;
        using System.Text.Json.Serialization;
        using Aspose.Cli.Sdk.Operations;
        namespace Sample;

        public static class Modes { public const string Fast = "fast"; public const string Slow = "slow"; }

        """;

    [Fact]
    public void Vocabulary_GeneratesDescriptorsHandlerAndDispatch()
    {
        GeneratorDriverRunResult result = Run(
            """
            /// <summary>Sample operations.</summary>
            [OperationVocabulary("https://example.test/ops.schema.json", MaximumOperations = 4, JsonContext = typeof(SampleJsonContext))]
            public abstract partial record SampleOp : Aspose.Cli.Sdk.Contracts.BoundedOperation;

            [ExactlyOneOf("path", "all")]
            public abstract record TargetOp : SampleOp
            {
                [InputPath] public string? Path { get; init; }
                public bool All { get; init; }
                [AllowedValues(typeof(Modes))] public string? Pace { get; init; }
                /// <summary>The target sheet.</summary>
                public virtual string? Sheet { get; init; }
            }

            /// <summary>Moves <c>pages</c>
            /// quickly.</summary>
            [Operation("move")]
            public sealed record MoveOp : TargetOp
            {
                /// <summary>The source sheet.</summary>
                public override string? Sheet { get; init; }
                [AllowedValues(typeof(Modes))] public string Mode { get; init; } = Modes.Fast;
                public uint Count { get; init; }
                public IReadOnlyList<string> Tags { get; init; } = new string[0];
                [HexColor] public IReadOnlyList<string>? Colors { get; init; }
                public IReadOnlyList<uint>? Counts { get; init; }
            }

            [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
            [JsonSerializable(typeof(MoveOp))]
            public partial class SampleJsonContext : JsonSerializerContext;
            """);

        Assert.DoesNotContain(result.Diagnostics, static diagnostic => diagnostic.Id == "APCLI012");
        string source = Assert.Single(Assert.Single(result.Results).GeneratedSources).SourceText.ToString();
        Assert.Contains("TResult Apply(global::Sample.MoveOp operation);", source, StringComparison.Ordinal);
        Assert.Contains("global::Sample.MoveOp operation => handler.Apply(operation),", source, StringComparison.Ordinal);
        Assert.Contains("Description = \"Moves pages quickly.\"", source, StringComparison.Ordinal);
        // Inherited members come first and the base record's rule applies to the operation.
        Assert.True(
            source.IndexOf("Name = \"path\"", StringComparison.Ordinal) < source.IndexOf("Name = \"mode\"", StringComparison.Ordinal),
            "Inherited members must precede the operation's own members.");
        // An override restates the inherited member's text for its record, in the member's place.
        Assert.Contains("Name = \"sheet\", Description = \"The source sheet.\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("The target sheet.", source, StringComparison.Ordinal);
        Assert.Equal(1, source.Split("Name = \"sheet\"").Length - 1);
        Assert.Contains("Constraints = [new global::Aspose.Cli.Sdk.Operations.ExactlyOneOfAttribute(new string[] { (string)\"path\", (string)\"all\" })]", source, StringComparison.Ordinal);
        Assert.Contains("AllowedValuesAttribute(new object[] { \"fast\", \"slow\" })", source, StringComparison.Ordinal);
        Assert.Contains("Name = \"count\", Value = new() { Kind = global::Aspose.Cli.Sdk.Operations.OperationValueKind.Integer }, Default = \"0\", Constraints = [new global::Aspose.Cli.Sdk.Operations.MinimumAttribute(0)]", source, StringComparison.Ordinal);
        Assert.Contains("Name = \"tags\", Value = new() { Kind = global::Aspose.Cli.Sdk.Operations.OperationValueKind.Array, Items = new() { Kind = global::Aspose.Cli.Sdk.Operations.OperationValueKind.String } }, Default = \"[]\"", source, StringComparison.Ordinal);
        Assert.Contains("ResolveInputPaths = static (operation, resolve) =>", source, StringComparison.Ordinal);
        // A scalar constraint on a list is placed on its items.
        Assert.Contains("Constraints = [new global::Aspose.Cli.Sdk.Operations.HexColorAttribute() { Depth = 1 }]", source, StringComparison.Ordinal);
        Assert.Contains("Constraints = [new global::Aspose.Cli.Sdk.Operations.MinimumAttribute(0) { Depth = 1 }]", source, StringComparison.Ordinal);
    }

    [Fact]
    public void OverrideWithConstraintOrDefault_IsReported()
    {
        GeneratorDriverRunResult result = Run(
            """
            [OperationVocabulary("https://example.test/ops.schema.json", MaximumOperations = 4, JsonContext = typeof(OverrideJsonContext))]
            public abstract partial record OverrideOp : Aspose.Cli.Sdk.Contracts.BoundedOperation;

            public abstract record BaseOp : OverrideOp
            {
                /// <summary>The base sheet.</summary>
                public virtual string? Sheet { get; init; }
                public virtual string? Title { get; init; }
                public virtual string? Name { get; init; }
                public virtual string? Label { get; init; }
                public virtual string? Code { get; init; }
            }

            [Operation("restate")]
            public sealed record RestateOp : BaseOp
            {
                /// <summary>The source sheet.</summary>
                public override string? Sheet { get; init; }
                [MinLength(1)] public override string? Title { get; init; }
                public override string? Name { get; init; } = "none";
                public required override string? Label { get; init; }
                public override string? Code { get => "fixed"; init { } }
            }

            [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
            [JsonSerializable(typeof(RestateOp))]
            public partial class OverrideJsonContext : JsonSerializerContext;
            """);

        (string Message, string At)[] reported = [.. result.Diagnostics
            .Where(static diagnostic => diagnostic.Id == "APCLI012" && diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(static diagnostic => (diagnostic.GetMessage(), diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan)))];
        const string Restates = "an override restates only the summary; state the rest on the base member";
        Assert.Contains(reported, static item => item.Message.Contains("'RestateOp.Title': " + Restates, StringComparison.Ordinal) && item.At == "Title");
        Assert.Contains(reported, static item => item.Message.Contains("'RestateOp.Name': " + Restates, StringComparison.Ordinal) && item.At == "Name");
        Assert.Contains(reported, static item => item.Message.Contains("'RestateOp.Label': " + Restates, StringComparison.Ordinal) && item.At == "Label");
        Assert.Contains(reported, static item => item.Message.Contains("'RestateOp.Code': " + Restates, StringComparison.Ordinal) && item.At == "Code");
        // A summary is documentation, not a constraint, so restating it stays valid.
        Assert.DoesNotContain(reported, static item => item.At == "Sheet");
        Assert.Equal(4, reported.Length);
    }

    [Fact]
    public void IncompleteContract_IsReportedWhereItIsDeclared()
    {
        GeneratorDriverRunResult result = Run(
            """
            [OperationVocabulary("https://example.test/ops.schema.json", MaximumOperations = 4, JsonContext = typeof(BadJsonContext))]
            public abstract partial record BadOp : Aspose.Cli.Sdk.Contracts.BoundedOperation;

            public static class Letters { public const char A = 'a'; }

            public sealed record Box { public double Width { get; init; } }

            [ExactlyOneOf("missing", "title")]
            [DependentRequired("title", "name")]
            [AtLeastOneOf("hint")]
            [MinProperties(1)]
            [PresentWhen("name", "absent", "x")]
            [Operation("broken")]
            public sealed record BrokenOp : BadOp
            {
                public required string Title { get; init; }
                public string? Op { get; init; }
                public string? Name { get; init; }
                [JsonPropertyName("name")] public string? Label { get; init; }
                [JsonIgnore] public string? Hidden { get; init; }
                public string Note { get; init; }
                public IReadOnlyList<string> Tags { get; init; } = new List<string> { "a" };
                [AllowedValues(typeof(Letters))] public string? Letter { get; init; }
                public Box? First { get; init; }
                public Other.Box? Second { get; init; }
                public string? Hint { get; init; } = "none";
                public List<string>? Items { get; init; }
                [MinLength(1)] public int? Count { get; init; }
                [MinItems(1, Depth = 1)] public IReadOnlyList<string>? Lines { get; init; }
            }

            [Operation("positional")]
            public sealed record PositionalOp(string Name) : BadOp;

            public sealed class Loose { [AllowedValues(typeof(Modes))] public string? Mode { get; init; } }

            [JsonSerializable(typeof(int))]
            public partial class BadJsonContext : JsonSerializerContext;

            public static class Other { public sealed record Box { public double Depth { get; init; } } }
            """);

        (string Message, string At)[] reported = [.. result.Diagnostics
            .Where(static diagnostic => diagnostic.Id == "APCLI012" && diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(static diagnostic => (diagnostic.GetMessage(), diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan)))];
        Assert.Contains(reported, static item => item.Message.Contains("must use camelCase property names", StringComparison.Ordinal) && item.At == "BadJsonContext");
        Assert.Contains(reported, static item => item.Message.Contains("'BrokenOp' is missing from JSON context", StringComparison.Ordinal) && item.At == "BrokenOp");
        Assert.Contains(reported, static item => item.Message.Contains("names 'missing', which is not a member of 'BrokenOp'", StringComparison.Ordinal) && item.At.StartsWith("ExactlyOneOf", StringComparison.Ordinal));
        Assert.Contains(reported, static item => item.Message.Contains("counts 'title', which must be nullable without a default or a Boolean", StringComparison.Ordinal) && item.At.StartsWith("ExactlyOneOf", StringComparison.Ordinal));
        Assert.Contains(reported, static item => item.Message.Contains("counts 'title', which must be nullable without a default.", StringComparison.Ordinal) && item.At.StartsWith("DependentRequired", StringComparison.Ordinal));
        Assert.Contains(reported, static item => item.Message.Contains("counts 'hint', which must be nullable without a default", StringComparison.Ordinal) && item.At.StartsWith("AtLeastOneOf", StringComparison.Ordinal));
        Assert.Contains(reported, static item => item.Message.Contains("[MinProperties] on 'BrokenOp' would also count the operation's op and id", StringComparison.Ordinal) && item.At.StartsWith("MinProperties", StringComparison.Ordinal));
        Assert.Contains(reported, static item => item.Message.Contains("names 'absent', which is not a member of 'BrokenOp'", StringComparison.Ordinal) && item.At.StartsWith("PresentWhen", StringComparison.Ordinal));
        Assert.Contains(reported, static item => item.Message.Contains("the wire name 'op' belongs to the operation envelope", StringComparison.Ordinal) && item.At == "Op");
        Assert.Contains(reported, static item => item.Message.Contains("remove [JsonPropertyName] and [JsonIgnore]", StringComparison.Ordinal) && item.At == "Label");
        Assert.Contains(reported, static item => item.Message.Contains("remove [JsonPropertyName] and [JsonIgnore]", StringComparison.Ordinal) && item.At == "Hidden");
        Assert.Contains(reported, static item => item.Message.Contains("is not a supported contract type; use a scalar", StringComparison.Ordinal) && item.At == "Items");
        Assert.Contains(reported, static item => item.Message.Contains("[MinLength] applies to String values, and the member has none at depth 0 or below", StringComparison.Ordinal) && item.At.StartsWith("MinLength", StringComparison.Ordinal));
        Assert.Contains(reported, static item => item.Message.Contains("[MinItems] applies to Array values, and the member has none at depth 1.", StringComparison.Ordinal) && item.At.StartsWith("MinItems", StringComparison.Ordinal));
        Assert.Contains(reported, static item => item.Message.Contains("needs 'required' or an initializer", StringComparison.Ordinal) && item.At == "Note");
        Assert.Contains(reported, static item => item.Message.Contains("an empty object or collection", StringComparison.Ordinal) && item.At == "Tags");
        Assert.Contains(reported, static item => item.Message.Contains("'Letters' must declare public string", StringComparison.Ordinal) && item.At.StartsWith("AllowedValues", StringComparison.Ordinal));
        Assert.Contains(reported, static item => item.Message.Contains("the schema definition 'box', which 'Box' already has", StringComparison.Ordinal) && item.At == "Second");
        Assert.Contains(reported, static item => item.Message.Contains("not as a positional record parameter", StringComparison.Ordinal));
        Assert.Contains(reported, static item => item.Message.Contains("'Loose.Mode': [AllowedValues(typeof(...))] is expanded only", StringComparison.Ordinal) && item.At.StartsWith("AllowedValues", StringComparison.Ordinal));
        Assert.Empty(Assert.Single(result.Results).GeneratedSources);
    }

    private static GeneratorDriverRunResult Run(string source)
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            "Sample.Product",
            [CSharpSyntaxTree.ParseText(Prelude + source, new CSharpParseOptions(LanguageVersion.Preview))],
            RoslynTestSupport.PlatformReferences().Append(MetadataReference.CreateFromFile(typeof(OperationCatalog<>).Assembly.Location)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        return CSharpGeneratorDriver.Create(new OperationContractGenerator()).RunGenerators(compilation).GetRunResult();
    }
}
