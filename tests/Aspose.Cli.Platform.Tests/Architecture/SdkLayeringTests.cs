using Aspose.Cli.TestKit;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// Reads the SDK source with the Roslyn semantic model and checks its namespace dependency graph:
/// it has no cycles, and every reference points down the declared layering.
/// </summary>
public sealed class SdkLayeringTests
{
    private const string Root = "Aspose.Cli.Sdk";

    /// <summary>
    /// The SDK layers from the bottom up. A namespace may reference only namespaces in a lower
    /// layer; namespaces that share a layer are independent of each other. Every SDK namespace
    /// is listed, so a new one needs a decision about where it sits.
    /// </summary>
    /// <remarks>
    /// <list type="number">
    /// <item>The distribution identity, and FilePaths, by which every layer compares local paths.</item>
    /// <item>Contracts: the serialized result, error and warning shapes, the value constraints
    /// and schema attributes they declare, and the descriptors the contract generator extracts
    /// from them. They declare what a result says, not how it is checked or produced, so they
    /// reference no diagnostics catalog, Serialization type or operation schema. A converter that only fixes the wire
    /// form of a contract value, such as the UTC timestamp converter, lives with the
    /// contracts that name it in their [JsonConverter] attributes.</item>
    /// <item>Errors: the error vocabulary and its factories over the contract shapes. Limits
    /// and name suggestions it reports are passed in or live here, not read from IO or
    /// Text.</item>
    /// <item>Primitives built on errors: text search and name suggestions, cell and page
    /// addressing, configuration paths, view manifests and the human-readable output
    /// helpers.</item>
    /// <item>IO.Files: the one open policy for reading an input file.</item>
    /// <item>Formats: the format declarations and the byte-signature recognition of input
    /// files, on which Extensibility builds products and their routes.</item>
    /// <item>IO: files, outputs, backups and publication, the operation deadline they run
    /// under, and the worker output session and manifest store that record a worker's
    /// outputs.</item>
    /// <item>Execution (worker output publication, built on IO) and Rendering (font profiles
    /// and pixel budgets).</item>
    /// <item>Serialization: the shared JSON contexts and converters, and the result schemas
    /// written from the contract descriptors. Converters for the operation vocabulary belong to
    /// Operations.</item>
    /// <item>Operations: the operation vocabulary, its catalog and its runner.</item>
    /// <item>Services the products bind: licensing, diagnostics and font checks. They
    /// take product data as values, never a Product* type from Extensibility.</item>
    /// <item>Results: the write pipeline (OutputPipeline, OutputSet) and the envelope parts it fills.</item>
    /// <item>Extensibility: product modules, definitions and the catalog, which compose
    /// everything below.</item>
    /// <item>Commanding: the System.CommandLine vocabulary product commands share, built on
    /// product definitions.</item>
    /// </list>
    /// </remarks>
    private static readonly string[][] Layers =
    [
        [Root],
        [$"{Root}.Contracts"],
        [$"{Root}.Errors"],
        [$"{Root}.Text", $"{Root}.Addressing", $"{Root}.Configuration", $"{Root}.Views", $"{Root}.Extensibility.Output"],
        [$"{Root}.IO.Files"],
        [$"{Root}.Formats"],
        [$"{Root}.IO"],
        [$"{Root}.Execution", $"{Root}.Rendering"],
        [$"{Root}.Serialization"],
        [$"{Root}.Operations"],
        [$"{Root}.Licensing", $"{Root}.Diagnostics"],
        [$"{Root}.Results"],
        [$"{Root}.Extensibility"],
        [$"{Root}.Extensibility.Commanding"],
    ];

    private static readonly Lazy<NamespaceGraph> Graph = new(BuildGraph);

    [Fact]
    public void SdkNamespaces_HaveNoDependencyCycle()
    {
        NamespaceGraph graph = Graph.Value;
        string[] cycles = graph.StronglyConnectedComponents()
            .Where(static component => component.Count > 1)
            .Select(graph.DescribeCycle)
            .ToArray();

        Assert.True(
            cycles.Length == 0,
            "The SDK namespace graph has cycles; each lists the edges among its namespaces with one example reference:"
            + Environment.NewLine
            + string.Join(Environment.NewLine + Environment.NewLine, cycles));
    }

    [Fact]
    public void SdkNamespaces_ReferenceOnlyLowerLayers()
    {
        NamespaceGraph graph = Graph.Value;
        Dictionary<string, int> layerOf = Layers
            .SelectMany(static (layer, index) => layer.Select(name => (name, index)))
            .ToDictionary(static entry => entry.name, static entry => entry.index, StringComparer.Ordinal);

        string[] undeclared = graph.Namespaces
            .Where(name => !layerOf.ContainsKey(name))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.True(
            undeclared.Length == 0,
            "Declare the layer of every SDK namespace in SdkLayeringTests.Layers: " + string.Join(", ", undeclared));

        string[] stale = layerOf.Keys
            .Where(name => !graph.Namespaces.Contains(name))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.True(
            stale.Length == 0,
            "SdkLayeringTests.Layers names namespaces the SDK no longer declares: " + string.Join(", ", stale));

        string[] violations = graph.Edges
            .Where(edge => layerOf[edge.Key.From] <= layerOf[edge.Key.To])
            .OrderBy(static edge => edge.Key.From, StringComparer.Ordinal)
            .ThenBy(static edge => edge.Key.To, StringComparer.Ordinal)
            .Select(edge =>
                $"{Short(edge.Key.From)} (layer {layerOf[edge.Key.From] + 1}) -> {Short(edge.Key.To)} "
                + $"(layer {layerOf[edge.Key.To] + 1}): {edge.Value[0]}")
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "These SDK references point up or across the declared layering:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void DependencyGraph_SeesTheWholeSdk()
    {
        NamespaceGraph graph = Graph.Value;

        // A source the semantic model cannot bind would silently drop its edges.
        Assert.True(
            graph.UnboundErrors.Length == 0,
            "The SDK source does not bind without the build's generators:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, graph.UnboundErrors));
        Assert.Contains($"{Root}.Extensibility", graph.Namespaces);
        Assert.Contains(
            graph.Edges.Keys,
            static edge => edge.From == $"{Root}.Extensibility" && edge.To == $"{Root}.Contracts");
    }

    private static NamespaceGraph BuildGraph() =>
        NamespaceGraph.Build(
            Root,
            NamespaceGraph.SourceFiles(Path.Combine(RepositoryPaths.Root, "src", Root))
                .Append(Path.Combine(RepositoryPaths.Root, "eng", "generated", "DistributionInfo.g.cs")),
            RoslynTestSupport.PlatformReferences()
                .Add(MetadataReference.CreateFromFile(typeof(System.CommandLine.Command).Assembly.Location))
                .Add(MetadataReference.CreateFromFile(typeof(SkiaSharp.SKCodec).Assembly.Location)));

    private static string Short(string name) => Graph.Value.Short(name);
}
