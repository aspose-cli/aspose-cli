using Aspose.Cli.TestKit;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// Reads the SDK source with the Roslyn semantic model and checks its namespace dependency graph:
/// apart from the listed exceptions it has no cycles, and every reference points down the
/// declared layering. IO is checked by its sub-areas, each a namespace of its own.
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
    /// <item>IO.Files: one file at a time: the open policy for reading an input file, the
    /// sharing-violation probe, a file's physical identity, fingerprint and publication
    /// snapshot, and the owned delete that removes only the file it captured.</item>
    /// <item>Formats: the format declarations and the byte-signature recognition of input
    /// files, on which Extensibility builds products and their routes; and IO.Budgets: the
    /// invocation's resource ledger, the operation deadline, the input-size and output-set
    /// limits and the streams that charge the ledger.</item>
    /// <item>IO.Paths: resolving and validating local paths, and the user storage roots.</item>
    /// <item>IO.Input: opening and loading input files, their protection and passwords, and
    /// the resources a document references.</item>
    /// <item>IO.Output: what an output is (path, format, parts, protection, the write
    /// precondition) and the file objects a result reports.</item>
    /// <item>IO.Publication: staging, atomic publication, its journal and recovery, the
    /// process identity that recovery checks, and the worker output session and manifest store
    /// that record a worker's outputs.</item>
    /// <item>IO.Extraction: the guard and budgets of an extraction's output files.</item>
    /// <item>Execution (worker output publication, built on IO), Rendering (font profiles
    /// and pixel budgets) and Serialization: the shared JSON contexts and converters, and the
    /// result schemas written from the contract descriptors. Converters for the operation
    /// vocabulary belong to Operations.</item>
    /// <item>Operations: the operation vocabulary, its catalog and its runner; and the
    /// services the products bind: licensing, diagnostics and font checks. They take product
    /// data as values, never a Product* type from Extensibility.</item>
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
        [$"{Root}.Formats", $"{Root}.IO.Budgets"],
        [$"{Root}.IO.Paths"],
        [$"{Root}.IO.Input"],
        [$"{Root}.IO.Output"],
        [$"{Root}.IO.Publication"],
        [$"{Root}.IO.Extraction"],
        [$"{Root}.Execution", $"{Root}.Rendering", $"{Root}.Serialization"],
        [$"{Root}.Operations", $"{Root}.Licensing", $"{Root}.Diagnostics"],
        [$"{Root}.Results"],
        [$"{Root}.Extensibility"],
        [$"{Root}.Extensibility.Commanding"],
    ];

    // rule-exemptions: begin
    /// <summary>
    /// The references allowed against the layering, each by the type that holds it and the
    /// namespace it reaches, with the reason it cannot point down.
    /// </summary>
    private static readonly (string Owner, string To, string Reason)[] Exceptions =
    [
        (
            $"{Root}.IO.Budgets.ResourceBudgetLedger",
            $"{Root}.IO.Input",
            "The ledger owns the bounded input reads of its invocation (Inputs), which charge it."),
        (
            $"{Root}.IO.Budgets.ResourceBudgetLedger",
            $"{Root}.IO.Publication",
            "The ledger carries the worker output session its invocation publishes into (OutputSession)."),
    ];
    // rule-exemptions: end

    private static readonly Lazy<NamespaceGraph> Graph = new(BuildGraph);

    [Fact]
    public void SdkNamespaces_HaveNoDependencyCycle()
    {
        NamespaceGraph graph = Graph.Value.Without(Excepted);
        string[] cycles = graph.StronglyConnectedComponents()
            .Where(static component => component.Count > 1)
            .Select(graph.DescribeCycle)
            .ToArray();

        Assert.True(
            cycles.Length == 0,
            "Without SdkLayeringTests.Exceptions the SDK namespace graph has cycles; each lists the edges among its namespaces with one example reference:"
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

        string[] violations = graph.Without(Excepted).Edges
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
    public void LayeringExceptions_AreEachStillNeeded()
    {
        NamespaceGraph graph = Graph.Value;
        string[] unused = Exceptions
            .Where(exception => !graph.Edges.Values.SelectMany(static references => references).Any(
                reference => reference.Owner.ToDisplayString() == exception.Owner
                    && reference.Target.ContainingNamespace.ToDisplayString() == exception.To))
            .Select(static exception => $"{exception.Owner} -> {exception.To}")
            .ToArray();

        Assert.True(
            unused.Length == 0,
            "Remove the SdkLayeringTests.Exceptions no reference needs any more: " + string.Join(", ", unused));
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

    private static bool Excepted(NamespaceReference reference) =>
        Exceptions.Any(exception =>
            reference.Owner.ToDisplayString() == exception.Owner
            && reference.Target.ContainingNamespace.ToDisplayString() == exception.To);

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
