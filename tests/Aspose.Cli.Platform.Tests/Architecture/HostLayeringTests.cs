using System.Reflection;
using System.Runtime.InteropServices;
using Aspose.Cli.Host;
using Aspose.Cli.TestKit;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// Reads the Host source with the Roslyn semantic model and checks that every reference
/// between its subsystems points down the declared layering. A reference counts however it
/// is written: a using directive, a global using or a fully qualified name.
/// </summary>
public sealed class HostLayeringTests
{
    private const string Root = "Aspose.Cli.Host";

    /// <summary>
    /// The Host subsystems from the bottom up. A namespace may reference only namespaces in a
    /// lower layer; namespaces that share a layer are independent of each other. Every Host
    /// namespace is listed, so a new one needs a decision about where it sits.
    /// </summary>
    /// <remarks>
    /// <list type="number">
    /// <item>Leaves: the Host result contracts, the local service runtime (loopback HTTP,
    /// control channels, owned processes, Windows PowerShell) and the product routing over the
    /// catalog.</item>
    /// <item>The table renderer and the Skill and docs catalogs, over the contracts.</item>
    /// <item>Output: the writers for each output mode.</item>
    /// <item>Invocation: parsing, the command context and composition root, execution and
    /// the process boundary every command runs under.</item>
    /// <item>Services commands call: licensing (and the startup license notice), updating and
    /// the MCP server.</item>
    /// <item>The root: CliHost, the process entry, which reaches the commands only through
    /// HostContext.</item>
    /// <item>Viewer: view bundles, the page around them and the warm render worker with the
    /// frame protocol it serves.</item>
    /// <item>The viewer service, which renders through that worker, and review, which renders
    /// in process.</item>
    /// <item>App: the local Web App, mounted on the viewer service's origin.</item>
    /// <item>Commands: the command adapters, which compose everything below.</item>
    /// </list>
    /// </remarks>
    private static readonly string[][] Layers =
    [
        [$"{Root}.Contracts", $"{Root}.LocalServices", $"{Root}.Catalog"],
        [$"{Root}.Output.Rendering", $"{Root}.Skills"],
        [$"{Root}.Output"],
        [$"{Root}.Invocation"],
        [$"{Root}.Licensing", $"{Root}.Updating", $"{Root}.Mcp"],
        [Root],
        [$"{Root}.Viewer"],
        [$"{Root}.ViewerService", $"{Root}.Review"],
        [$"{Root}.App"],
        [$"{Root}.Commands"],
    ];

    // rule-exemptions: begin
    /// <summary>
    /// The references allowed against the layering, each by the type that holds it and the
    /// namespace it reaches, with the reason it cannot point down.
    /// </summary>
    private static readonly (string Owner, string To, string Reason)[] Exceptions =
    [
        (
            $"{Root}.Invocation.HostContext",
            $"{Root}.Commands",
            "The composition root builds the command tree and the schema catalog that every "
                + "lower subsystem reaches through it."),
    ];
    // rule-exemptions: end

    private static readonly Lazy<NamespaceGraph> Graph = new(BuildGraph);

    [Fact]
    public void HostNamespaces_ReferenceOnlyLowerLayers()
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
            "Declare the layer of every Host namespace in HostLayeringTests.Layers: " + string.Join(", ", undeclared));

        string[] stale = layerOf.Keys
            .Where(name => !graph.Namespaces.Contains(name))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.True(
            stale.Length == 0,
            "HostLayeringTests.Layers names namespaces the Host no longer declares: " + string.Join(", ", stale));

        NamespaceGraph checkedGraph = graph.Without(Excepted);
        string[] violations = checkedGraph.Edges
            .Where(edge => layerOf[edge.Key.From] <= layerOf[edge.Key.To])
            .OrderBy(static edge => edge.Key.From, StringComparer.Ordinal)
            .ThenBy(static edge => edge.Key.To, StringComparer.Ordinal)
            .SelectMany(edge => edge.Value.Select(reference =>
                $"{graph.Short(edge.Key.From)} (layer {layerOf[edge.Key.From] + 1}) -> {graph.Short(edge.Key.To)} "
                + $"(layer {layerOf[edge.Key.To] + 1}): {reference}"))
            .ToArray();
        string[] cycles = checkedGraph.StronglyConnectedComponents()
            .Where(static component => component.Count > 1)
            .Select(checkedGraph.DescribeCycle)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "These Host references point up or across the declared layering:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations)
            + (cycles.Length == 0
                ? string.Empty
                : Environment.NewLine + Environment.NewLine + "The namespace graph has cycles:"
                    + Environment.NewLine + string.Join(Environment.NewLine, cycles)));
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
            "Remove the HostLayeringTests.Exceptions no reference needs any more: " + string.Join(", ", unused));
    }

    [Fact]
    public void DependencyGraph_SeesTheWholeHost()
    {
        NamespaceGraph graph = Graph.Value;

        // A source the semantic model cannot bind would silently drop its edges.
        Assert.True(
            graph.UnboundErrors.Length == 0,
            "The Host source does not bind without the build's generators:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, graph.UnboundErrors));
        Assert.Contains($"{Root}.Commands", graph.Namespaces);
        // A fully qualified name counts as much as a using directive.
        Assert.Contains(
            graph.Edges.GetValueOrDefault(($"{Root}.Invocation", $"{Root}.Commands"), []),
            static reference => reference.Owner.Name == "HostContext" && reference.Target.Name == "HostSchemaCatalog");
    }

    private static bool Excepted(NamespaceReference reference) =>
        Exceptions.Any(exception =>
            reference.Owner.ToDisplayString() == exception.Owner
            && reference.Target.ContainingNamespace.ToDisplayString() == exception.To);

    private static NamespaceGraph BuildGraph()
    {
        // The libraries the Host builds against, and theirs: a type the Host names may expose
        // one of a library's own dependencies.
        string runtime = RuntimeEnvironment.GetRuntimeDirectory();
        var libraries = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<Assembly>([typeof(CliHost).Assembly]);
        while (pending.TryDequeue(out Assembly? assembly))
        {
            foreach (AssemblyName name in assembly.GetReferencedAssemblies())
            {
                Assembly referenced = Assembly.Load(name);
                if (!referenced.Location.StartsWith(runtime, StringComparison.OrdinalIgnoreCase)
                    && libraries.Add(referenced.Location))
                {
                    pending.Enqueue(referenced);
                }
            }
        }

        return NamespaceGraph.Build(
            Root,
            NamespaceGraph.SourceFiles(Path.Combine(RepositoryPaths.Root, "src", Root)),
            RoslynTestSupport.PlatformReferences().AddRange(
                libraries.Select(static path => MetadataReference.CreateFromFile(path))));
    }
}
