using Aspose.Cli.Sdk.Analyzers;
using Aspose.Cli.Sdk.Contracts;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// The contract generator describes a product's result records from their declarations and
/// documentation, writes the descriptors onto the JSON context that lists them, and rejects a
/// record it cannot describe completely.
/// </summary>
public sealed class ResultContractGeneratorTests
{
    private const string Prelude =
        """
        using System.Collections.Generic;
        using System.Text.Json.Nodes;
        using System.Text.Json.Serialization;
        using Aspose.Cli.Sdk.Contracts;
        namespace Sample;

        public static class Kinds { public const string Plain = "plain"; public const string Bold = "bold"; }

        public static class CellTypes { public const string Empty = "empty"; public const string Number = "number"; public const string Text = "string"; public const string Error = "error"; }

        """;

    [Fact]
    public void ProductResult_IsDescribedOnItsJsonContext()
    {
        GeneratorDriverRunResult result = Run(
            """
            /// <summary>The pages rendered and the files written.</summary>
            public sealed record RenderResult() : ResultEnvelope("render-result", 2)
            {
                /// <summary>The kind of result.</summary>
                [JsonPropertyOrder(-50)]
                public string Kind { get; } = "render";

                /// <summary>Each written page, in page order.</summary>
                [MinItems(1)]
                public required IReadOnlyList<RenderedPage> Pages { get; init; }

                /// <summary>The resolution, in dots per inch.</summary>
                [Minimum(36), Maximum(1200)]
                public int? Dpi { get; init; }

                /// <summary>Document metadata by name; a value is null when the entry is empty.</summary>
                public IReadOnlyDictionary<string, string?>? Metadata { get; init; }

                /// <summary>When the render finished.</summary>
                public required System.DateTimeOffset Finished { get; init; }

                /// <summary>Engine-specific detail.</summary>
                public JsonObject? Detail { get; init; }

                /// <summary>The style, or an engine name outside the vocabulary.</summary>
                [AllowedValues(typeof(Kinds)), OpenEnum("^[a-z][A-Za-z0-9]*$")]
                public required string Style { get; init; }

                /// <summary>Whether a page was skipped.</summary>
                [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
                public bool Skipped { get; init; }

                [JsonIgnore]
                public bool Internal => Skipped;

                public required Cell First { get; init; }
            }

            /// <summary>One written page.</summary>
            public sealed record RenderedPage
            {
                /// <summary>The 1-based page number.</summary>
                public required uint Page { get; init; }

                /// <summary>The written file's path.</summary>
                [JsonPropertyName("file")]
                public required string Path { get; init; }
            }

            /// <summary>A cell: its type decides its value.</summary>
            public sealed record Cell
            {
                /// <summary>The stored type.</summary>
                [AllowedValues(typeof(CellTypes))]
                public required string T { get; init; }

                /// <summary>The stored value.</summary>
                [OneOfBy("t", "empty")]
                [OneOfBy("t", "number", Type = "number")]
                [OneOfBy("t", "string", "error", Type = "string")]
                public object? V { get; init; }
            }

            /// <summary>A shared block.</summary>
            /// <param name="Name">The block's name.</param>
            [SchemaId("block")]
            public sealed record Block(string Name);

            [JsonSerializable(typeof(RenderResult))]
            [JsonSerializable(typeof(Block))]
            internal sealed partial class ProductJsonContext : JsonSerializerContext;
            """);

        Assert.Empty(result.Diagnostics.Where(static diagnostic => diagnostic.Id == "APCLI013"));
        string source = Assert.Single(Assert.Single(result.Results).GeneratedSources).SourceText.ToString();
        const string Kind = "global::Aspose.Cli.Sdk.Contracts.ResultValueKind.";
        Assert.Contains("partial class ProductJsonContext : global::Aspose.Cli.Sdk.Contracts.IResultSchemaSource", source, StringComparison.Ordinal);
        Assert.Contains(
            "new() { Type = typeof(global::Sample.RenderResult), Description = \"The pages rendered and the files written.\", Base = typeof(global::Aspose.Cli.Sdk.Contracts.ResultEnvelope), SchemaId = \"render-result\", SchemaVersion = 2, Properties = [",
            source,
            StringComparison.Ordinal);
        Assert.Contains($"new() {{ Name = \"kind\", Description = \"The kind of result.\", Value = new() {{ Kind = {Kind}String }}, Required = true, Const = \"\\\"render\\\"\", Order = -50 }}", source, StringComparison.Ordinal);
        Assert.Contains($"Name = \"pages\", Description = \"Each written page, in page order.\", Value = new() {{ Kind = {Kind}Array, Items = new() {{ Kind = {Kind}Record, Record = typeof(global::Sample.RenderedPage) }} }}, Required = true, Constraints = [new global::Aspose.Cli.Sdk.Contracts.MinItemsAttribute((int)1)]", source, StringComparison.Ordinal);
        Assert.Contains($"Name = \"dpi\", Description = \"The resolution, in dots per inch.\", Value = new() {{ Kind = {Kind}Integer }}, Constraints = [new global::Aspose.Cli.Sdk.Contracts.MinimumAttribute((double)36D), new global::Aspose.Cli.Sdk.Contracts.MaximumAttribute((double)1200D)]", source, StringComparison.Ordinal);
        Assert.Contains($"Value = new() {{ Kind = {Kind}Map, Items = new() {{ Kind = {Kind}String, Nullable = true }} }} }}", source, StringComparison.Ordinal);
        Assert.Contains($"Name = \"finished\", Description = \"When the render finished.\", Value = new() {{ Kind = {Kind}String, Format = \"date-time\" }}, Required = true", source, StringComparison.Ordinal);
        Assert.Contains($"Name = \"detail\", Description = \"Engine-specific detail.\", Value = new() {{ Kind = {Kind}Object }} }}", source, StringComparison.Ordinal);
        Assert.Contains("Constraints = [new global::Aspose.Cli.Sdk.Contracts.AllowedValuesAttribute(new object[] { \"plain\", \"bold\" })], OpenPattern = \"^[a-z][A-Za-z0-9]*$\"", source, StringComparison.Ordinal);
        // A member omitted at its default is optional; an ignored member is not serialized.
        Assert.Contains($"Name = \"skipped\", Description = \"Whether a page was skipped.\", Value = new() {{ Kind = {Kind}Boolean }} }}", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"internal\"", source, StringComparison.Ordinal);
        // A member without a summary is described by the record it references.
        Assert.Contains($"new() {{ Name = \"first\", Value = new() {{ Kind = {Kind}Record, Record = typeof(global::Sample.Cell) }}, Required = true }}", source, StringComparison.Ordinal);
        Assert.Contains($"Name = \"page\", Description = \"The 1-based page number.\", Value = new() {{ Kind = {Kind}Integer }}, Required = true, Constraints = [new global::Aspose.Cli.Sdk.Contracts.MinimumAttribute(0)]", source, StringComparison.Ordinal);
        Assert.Contains("Name = \"file\", Description = \"The written file's path.\"", source, StringComparison.Ordinal);
        Assert.Contains(
            "Cases = [new global::Aspose.Cli.Sdk.Contracts.OneOfByAttribute((string)\"t\", new string[] { (string)\"empty\" }), "
            + "new global::Aspose.Cli.Sdk.Contracts.OneOfByAttribute((string)\"t\", new string[] { (string)\"number\" }) { Type = (string)\"number\" }, "
            + "new global::Aspose.Cli.Sdk.Contracts.OneOfByAttribute((string)\"t\", new string[] { (string)\"string\", (string)\"error\" }) { Type = (string)\"string\" }]",
            source,
            StringComparison.Ordinal);
        // A positional member is described by the record's <param>.
        Assert.Contains($"new() {{ Type = typeof(global::Sample.Block), Description = \"A shared block.\", SchemaId = \"block\", Properties = [\n                new() {{ Name = \"name\", Description = \"The block's name.\", Value = new() {{ Kind = {Kind}String }}, Required = true }},", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DocumentationReferenceToAProperty_IsDescribedByItsWireName()
    {
        GeneratorDriverRunResult result = Run(
            """
            /// <summary>A stamp: <see cref="Text"/> is drawn on <see cref="Spot.Page"/>, see <see cref="Spot"/>.</summary>
            public sealed record StampResult() : ResultEnvelope("stamp-result", 2)
            {
                /// <summary>The stamp text; present only with <see cref="P:Sample.StampResult.Where"/>.</summary>
                public required string Text { get; init; }

                /// <summary>Where it is drawn; it may add <see cref="Warnings"/>.</summary>
                public Spot? Where { get; init; }
            }

            /// <summary>A place on <paramref name="Page"/>, drawn at <see cref="Path"/>.</summary>
            /// <param name="Page">The 1-based page; <see cref="Path"/> names the file.</param>
            public sealed record Spot(int Page)
            {
                /// <summary>The file the place is on.</summary>
                [JsonPropertyName("file")]
                public required string Path { get; init; }
            }

            [JsonSerializable(typeof(StampResult))]
            internal sealed partial class ProductJsonContext : JsonSerializerContext;
            """);

        Assert.Empty(result.Diagnostics.Where(static diagnostic => diagnostic.Id == "APCLI013"));
        string source = Assert.Single(Assert.Single(result.Results).GeneratedSources).SourceText.ToString();
        // A property reference, plain, qualified or by id, renders as the JSON name; a type keeps its name.
        Assert.Contains("Description = \"A stamp: text is drawn on page, see Spot.\"", source, StringComparison.Ordinal);
        Assert.Contains("Description = \"The stamp text; present only with where.\"", source, StringComparison.Ordinal);
        // A member the record inherits from its envelope is found on the base.
        Assert.Contains("Description = \"Where it is drawn; it may add warnings.\"", source, StringComparison.Ordinal);
        // A [JsonPropertyName] wins over the camelCase name, for a see and a paramref alike.
        Assert.Contains("Description = \"A place on page, drawn at file.\"", source, StringComparison.Ordinal);
        Assert.Contains("Description = \"The 1-based page; file names the file.\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AlwaysPresentMembers_AreCarriedOnTheMember()
    {
        GeneratorDriverRunResult result = Run(
            """
            /// <summary>A read.</summary>
            public sealed record ReadResult() : ResultEnvelope("read-result", 2)
            {
                /// <summary>The file read.</summary>
                [AlwaysPresent("fingerprint")]
                public required SourceInfo Source { get; init; }

                /// <summary>The names read.</summary>
                [AlwaysPresent("fingerprint")]
                public IReadOnlyDictionary<string, SourceInfo>? Names { get; init; }
            }

            [JsonSerializable(typeof(ReadResult))]
            internal sealed partial class ProductJsonContext : JsonSerializerContext;
            """);

        Diagnostic error = Assert.Single(result.Diagnostics, static diagnostic => diagnostic.Id == "APCLI013");
        Assert.Contains("'ReadResult.Names'", error.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("must hold a record or an array of records", error.GetMessage(), StringComparison.Ordinal);

        result = Run(
            """
            /// <summary>A read.</summary>
            public sealed record ReadResult() : ResultEnvelope("read-result", 2)
            {
                /// <summary>The file read.</summary>
                [AlwaysPresent("fingerprint")]
                public required SourceInfo Source { get; init; }

                /// <summary>The files written.</summary>
                [AlwaysPresent("format")]
                public required IReadOnlyList<OutputInfo> Outputs { get; init; }
            }

            [JsonSerializable(typeof(ReadResult))]
            internal sealed partial class ProductJsonContext : JsonSerializerContext;
            """);

        Assert.Empty(result.Diagnostics.Where(static diagnostic => diagnostic.Id == "APCLI013"));
        string source = Assert.Single(Assert.Single(result.Results).GeneratedSources).SourceText.ToString();
        Assert.Contains("SchemaId = \"read-result\", SchemaVersion = 2, Properties = [", source, StringComparison.Ordinal);
        Assert.Contains("Record = typeof(global::Aspose.Cli.Sdk.Contracts.SourceInfo) }, Required = true, AlwaysPresent = [\"fingerprint\"] }", source, StringComparison.Ordinal);
        Assert.Contains("Record = typeof(global::Aspose.Cli.Sdk.Contracts.OutputInfo) } }, Required = true, AlwaysPresent = [\"format\"] }", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultOfAnSdkEnvelopeBase_IsPublished_AndOneOfALocalBaseIsNot()
    {
        GeneratorDriverRunResult result = Run(
            """
            /// <summary>A bounded read.</summary>
            public sealed record ReadResult() : WindowedResultEnvelope("read-result", 2);

            public abstract record LocalEnvelope : ResultEnvelope
            {
                protected LocalEnvelope(string schema)
                    : base(schema, 1)
                {
                }
            }

            public sealed record LocalResult() : LocalEnvelope("local");

            [JsonSerializable(typeof(ReadResult))]
            internal sealed partial class ProductJsonContext : JsonSerializerContext;
            """);

        Assert.Empty(result.Diagnostics.Where(static diagnostic => diagnostic.Id == "APCLI013"));
        string source = Assert.Single(Assert.Single(result.Results).GeneratedSources).SourceText.ToString();
        Assert.Contains("Base = typeof(global::Aspose.Cli.Sdk.Contracts.WindowedResultEnvelope), SchemaId = \"read-result\", SchemaVersion = 2", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LocalResult", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LocalEnvelope", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"https://example.test/legacy.schema.json\"")]
    [InlineData("Id")]
    public void ResultWithoutAConstantRelativeId_IsReported(string id)
    {
        GeneratorDriverRunResult result = Run(
            $$"""
            /// <summary>A result.</summary>
            public sealed record LegacyResult() : ResultEnvelope({{id}}, 2)
            {
                public static readonly string Id = "legacy-result";
            }

            [JsonSerializable(typeof(LegacyResult))]
            internal sealed partial class ProductJsonContext : JsonSerializerContext;
            """);

        Diagnostic error = Assert.Single(result.Diagnostics, static diagnostic => diagnostic.Id == "APCLI013");
        Assert.Contains("'LegacyResult' must pass the ResultEnvelope constructor a constant relative schema id", error.GetMessage(), StringComparison.Ordinal);
        Assert.Empty(Assert.Single(result.Results).GeneratedSources);
    }

    [Fact]
    public void IncompleteResult_IsReportedWhereItIsDeclared()
    {
        GeneratorDriverRunResult result = Run(
            """
            public enum Mode { Fast, Slow }

            /// <summary>A result.</summary>
            public sealed record BrokenResult() : ResultEnvelope("broken-result", 2)
            {
                public required string Undescribed { get; init; }

                /// <summary>An enum.</summary>
                public Mode Mode { get; init; }

                /// <summary>A record of another assembly that publishes no schema.</summary>
                public Aspose.Cli.Sdk.Views.ProductView? View { get; init; }

                /// <summary>An open enumeration without its values.</summary>
                [OpenEnum("^x$")]
                public string? Open { get; init; }

                /// <summary>A text.</summary>
                [Minimum(1)]
                public string? Text { get; init; }

                /// <summary>A cell.</summary>
                public BadCell? Cell { get; init; }

                /// <summary>A file, with members it cannot keep present.</summary>
                [AlwaysPresent("fingerprint", "path", "missing")]
                public SourceInfo? Source { get; init; }

                /// <summary>Open members beside an inherited one.</summary>
                public OpenBag? Bag { get; init; }

                /// <summary>A cell with two decided members.</summary>
                public TwoCaseCell? Twice { get; init; }
            }

            /// <summary>A base with one member.</summary>
            public record OpenBase
            {
                /// <summary>A member.</summary>
                public string? A { get; init; }
            }

            /// <summary>Open members.</summary>
            public sealed record OpenBag : OpenBase
            {
                /// <summary>The members.</summary>
                [System.Text.Json.Serialization.JsonExtensionData]
                public System.Text.Json.Nodes.JsonObject? Extra { get; init; }
            }

            /// <summary>A cell.</summary>
            public record OneCaseCell
            {
                /// <summary>The stored type.</summary>
                [AllowedValues(typeof(CellTypes))]
                public required string T { get; init; }

                /// <summary>The value.</summary>
                [OneOfBy("t", "empty", "number", "string", "error", Type = "string")]
                public object? V { get; init; }
            }

            /// <summary>A cell with a second decided member.</summary>
            public sealed record TwoCaseCell : OneCaseCell
            {
                /// <summary>Another value.</summary>
                [OneOfBy("t", "empty", "number", "string", "error", Type = "string")]
                public object? W { get; init; }
            }

            /// <summary>A cell.</summary>
            public sealed record BadCell
            {
                /// <summary>The stored type.</summary>
                [AllowedValues(typeof(CellTypes))]
                public required string T { get; init; }

                /// <summary>The value.</summary>
                [OneOfBy("t", "empty")]
                [OneOfBy("t", "number", "number", Type = "date")]
                public object? V { get; init; }
            }

            public static class Holder
            {
                /// <summary>A result its JSON context cannot name.</summary>
                private sealed record HiddenResult() : ResultEnvelope("hidden-result", 2);
            }

            /// <summary>Another result with the same id.</summary>
            public sealed record TwinResult() : ResultEnvelope("broken-result", 2);

            [SchemaId("Not Relative")]
            public sealed record Misnamed;

            [JsonSerializable(typeof(BrokenResult))]
            internal sealed class ProductJsonContext : JsonSerializerContext;
            """);

        (string Message, string At)[] reported = [.. result.Diagnostics
            .Where(static diagnostic => diagnostic.Id == "APCLI013" && diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(static diagnostic => (diagnostic.GetMessage(), diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan)))];
        Assert.Contains(reported, static item => item.Message.Contains("'BrokenResult.Undescribed': describe it with a documentation summary", StringComparison.Ordinal) && item.At == "Undescribed");
        Assert.Contains(reported, static item => item.Message.Contains("'BrokenResult.Mode': type 'Sample.Mode' is not a supported result type", StringComparison.Ordinal) && item.At == "Mode");
        Assert.Contains(reported, static item => item.Message.Contains("'ProductView' of another assembly is not a published result schema", StringComparison.Ordinal) && item.At == "View");
        Assert.Contains(reported, static item => item.Message.Contains("Result record 'HiddenResult' must be accessible", StringComparison.Ordinal) && item.At == "HiddenResult");
        Assert.Contains(reported, static item => item.Message.Contains("'BrokenResult.Open': [OpenEnum] extends the [AllowedValues] of a string member", StringComparison.Ordinal) && item.At == "Open");
        Assert.Contains(reported, static item => item.Message.Contains("'BrokenResult.Text': [Minimum] applies to Integer or Number values", StringComparison.Ordinal) && item.At.StartsWith("Minimum", StringComparison.Ordinal));
        Assert.Contains(reported, static item => item.Message.Contains("'BadCell.V': [OneOfBy] cases must cover each allowed value of 't' exactly once: empty, number, string, error", StringComparison.Ordinal) && item.At == "V");
        Assert.Contains(reported, static item => item.Message.Contains("'BadCell.V': [OneOfBy] Type must be one of: null, string, number, integer, boolean", StringComparison.Ordinal) && item.At == "V");
        Assert.Contains(reported, static item => item.Message.Contains("'BrokenResult.Source': [AlwaysPresent] names 'path', which is not an optional member of SourceInfo", StringComparison.Ordinal) && item.At == "Source");
        Assert.Contains(reported, static item => item.Message.Contains("'BrokenResult.Source': [AlwaysPresent] names 'missing', which is not an optional member of SourceInfo", StringComparison.Ordinal) && item.At == "Source");
        Assert.DoesNotContain(reported, static item => item.Message.Contains("names 'fingerprint'", StringComparison.Ordinal));
        Assert.Contains(reported, static item => item.Message.Contains("'OpenBag.Extra': extension data must be the record's only member", StringComparison.Ordinal) && item.At == "Extra");
        Assert.Contains(reported, static item => item.Message.Contains("'TwoCaseCell.W': only one member of a record may depend on a discriminator", StringComparison.Ordinal) && item.At == "W");
        Assert.Contains(reported, static item => item.Message.Contains("Result records 'BrokenResult' and 'TwinResult' both publish the schema 'broken-result'", StringComparison.Ordinal) && item.At == "TwinResult");
        Assert.Contains(reported, static item => item.Message.Contains("[SchemaId] on 'Misnamed' must name a relative id", StringComparison.Ordinal) && item.At == "Misnamed");
        Assert.Contains(reported, static item => item.Message.Contains("JSON context 'ProductJsonContext' lists result records, so it must be a top-level partial class.", StringComparison.Ordinal) && item.At == "ProductJsonContext");
        Assert.Empty(Assert.Single(result.Results).GeneratedSources);
    }

    [Fact]
    public void PublishedRecordOutsideEveryJsonContext_IsReported()
    {
        GeneratorDriverRunResult result = Run(
            """
            /// <summary>A result no JSON context lists.</summary>
            public sealed record LostResult() : ResultEnvelope("lost-result", 2);
            """);

        Assert.Contains(
            result.Diagnostics,
            static diagnostic => diagnostic.Id == "APCLI013" && diagnostic.GetMessage().Contains("List the result 'LostResult' in a JsonSerializerContext", StringComparison.Ordinal));
    }

    private static GeneratorDriverRunResult Run(string source)
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            "Sample.Product",
            [CSharpSyntaxTree.ParseText(Prelude + source, new CSharpParseOptions(LanguageVersion.Preview))],
            RoslynTestSupport.PlatformReferences().Append(MetadataReference.CreateFromFile(typeof(ResultEnvelope).Assembly.Location)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        return CSharpGeneratorDriver.Create(new OperationContractGenerator()).RunGenerators(compilation).GetRunResult();
    }
}
