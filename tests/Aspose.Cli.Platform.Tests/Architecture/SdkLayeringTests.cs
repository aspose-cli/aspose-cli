using System.Collections.Immutable;
using Aspose.Cli.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
    /// <item>The distribution identity.</item>
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
    /// <item>IO: files, outputs, backups and publication, the operation deadline they run
    /// under, and the worker output session and manifest store that record a worker's
    /// outputs. It also holds the format declarations and the byte-signature recognition
    /// of input files, on which Extensibility builds products and their routes.</item>
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
        [$"{Root}.IO"],
        [$"{Root}.Execution", $"{Root}.Rendering"],
        [$"{Root}.Serialization"],
        [$"{Root}.Operations"],
        [$"{Root}.Licensing", $"{Root}.Diagnostics"],
        [$"{Root}.Results"],
        [$"{Root}.Extensibility"],
        [$"{Root}.Extensibility.Commanding"],
    ];

    private static readonly Lazy<DependencyGraph> Graph = new(BuildGraph);

    [Fact]
    public void SdkNamespaces_HaveNoDependencyCycle()
    {
        DependencyGraph graph = Graph.Value;
        string[] cycles = StronglyConnectedComponents(graph)
            .Where(static component => component.Count > 1)
            .Select(component => DescribeCycle(graph, component))
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
        DependencyGraph graph = Graph.Value;
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
                + $"(layer {layerOf[edge.Key.To] + 1}): {edge.Value}")
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
        DependencyGraph graph = Graph.Value;

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

    private static DependencyGraph BuildGraph()
    {
        string source = Path.Combine(RepositoryPaths.Root, "src", Root);
        string separator = Path.DirectorySeparatorChar.ToString();
        CSharpParseOptions options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        List<SyntaxTree> trees = Directory
            .EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(source, path).Split(separator).Any(
                static part => part is "bin" or "obj"))
            .Append(Path.Combine(RepositoryPaths.Root, "eng", "generated", "DistributionInfo.g.cs"))
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), options, path))
            .ToList();
        // The project's implicit usings.
        trees.Add(CSharpSyntaxTree.ParseText(
            """
            global using System;
            global using System.Collections.Generic;
            global using System.IO;
            global using System.Linq;
            global using System.Net.Http;
            global using System.Threading;
            global using System.Threading.Tasks;
            """,
            options,
            "ImplicitUsings.g.cs"));

        CSharpCompilation compilation = CSharpCompilation.Create(
            Root,
            trees,
            RoslynTestSupport.PlatformReferences()
                .Add(MetadataReference.CreateFromFile(typeof(System.CommandLine.Command).Assembly.Location)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var namespaces = new HashSet<string>(StringComparer.Ordinal);
        var edges = new Dictionary<(string From, string To), string>();
        foreach (SyntaxTree tree in trees)
        {
            SemanticModel model = compilation.GetSemanticModel(tree);
            SyntaxNode root = tree.GetRoot();
            foreach (BaseTypeDeclarationSyntax declaration in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(declaration) is { } declared)
                {
                    namespaces.Add(declared.ContainingNamespace.ToDisplayString());
                }
            }

            foreach (SimpleNameSyntax name in root.DescendantNodes().OfType<SimpleNameSyntax>())
            {
                if (ReferencedType(model, name) is not { } target
                    || !SymbolEqualityComparer.Default.Equals(target.ContainingAssembly, compilation.Assembly)
                    || model.GetEnclosingSymbol(name.SpanStart) is not { } enclosing
                    || EnclosingType(enclosing) is not { } owner)
                {
                    continue;
                }

                string from = owner.ContainingNamespace.ToDisplayString();
                string to = target.ContainingNamespace.ToDisplayString();
                if (from == to || !from.StartsWith(Root, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!edges.ContainsKey((from, to)))
                {
                    int line = name.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    edges[(from, to)] =
                        $"{owner.Name} uses {target.Name} ({Path.GetRelativePath(RepositoryPaths.Root, tree.FilePath).Replace('\\', '/')}:{line})";
                }
            }
        }

        ImmutableArray<string> unbound = [.. compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error && !GeneratedMemberError(compilation, diagnostic))
            .Select(static diagnostic => diagnostic.ToString())
            .Take(20)];
        return new DependencyGraph(namespaces, edges, unbound);
    }

    // The build's source generators implement the [GeneratedRegex] partial methods and the
    // members of every JsonSerializerContext; without them only those members are missing.
    private static bool GeneratedMemberError(Compilation compilation, Diagnostic diagnostic)
    {
        if (diagnostic.Id == "CS8795")
        {
            return true;
        }

        if (diagnostic.Location.SourceTree is not { } tree
            || compilation.GetTypeByMetadataName("System.Text.Json.Serialization.JsonSerializerContext") is not { } context)
        {
            return false;
        }

        SemanticModel model = compilation.GetSemanticModel(tree);
        SyntaxNode node = tree.GetRoot().FindNode(diagnostic.Location.SourceSpan);
        INamedTypeSymbol? type = diagnostic.Id switch
        {
            "CS0534" or "CS7036" => node.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().FirstOrDefault() is { } declaration
                ? model.GetDeclaredSymbol(declaration)
                : null,
            "CS0117" or "CS1061" => node.AncestorsAndSelf().OfType<MemberAccessExpressionSyntax>().FirstOrDefault() is { } access
                ? model.GetTypeInfo(access.Expression).Type as INamedTypeSymbol
                    ?? model.GetSymbolInfo(access.Expression).Symbol as INamedTypeSymbol
                : null,
            _ => null,
        };
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, context))
            {
                return true;
            }
        }

        return false;
    }

    private static INamedTypeSymbol? ReferencedType(SemanticModel model, SimpleNameSyntax name)
    {
        SymbolInfo info = model.GetSymbolInfo(name);
        ISymbol? symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
        INamedTypeSymbol? type = symbol switch
        {
            INamedTypeSymbol named => named,
            IMethodSymbol { MethodKind: MethodKind.Constructor } constructor => constructor.ContainingType,
            IMethodSymbol { ReducedFrom: { } extension } => extension.ContainingType,
            IMethodSymbol method => method.ContainingType,
            IPropertySymbol or IFieldSymbol or IEventSymbol => symbol.ContainingType,
            _ => null,
        };
        return type is null ? null : Outermost(type.OriginalDefinition);
    }

    private static INamedTypeSymbol? EnclosingType(ISymbol symbol) =>
        symbol as INamedTypeSymbol is { } type ? Outermost(type)
        : symbol.ContainingType is { } containing ? Outermost(containing)
        : null;

    private static INamedTypeSymbol Outermost(INamedTypeSymbol type)
    {
        while (type.ContainingType is { } containing)
        {
            type = containing;
        }

        return type;
    }

    private static List<List<string>> StronglyConnectedComponents(DependencyGraph graph)
    {
        // Tarjan's algorithm over the namespaces in ordinal order, so the report is stable.
        Dictionary<string, string[]> successors = graph.Namespaces
            .ToDictionary(
                static name => name,
                name => graph.Edges.Keys.Where(edge => edge.From == name).Select(static edge => edge.To)
                    .Order(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        var low = new Dictionary<string, int>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        var onStack = new HashSet<string>(StringComparer.Ordinal);
        var components = new List<List<string>>();

        void Visit(string node)
        {
            index[node] = low[node] = index.Count;
            stack.Push(node);
            onStack.Add(node);
            foreach (string next in successors.GetValueOrDefault(node, []))
            {
                if (!index.ContainsKey(next))
                {
                    Visit(next);
                    low[node] = Math.Min(low[node], low[next]);
                }
                else if (onStack.Contains(next))
                {
                    low[node] = Math.Min(low[node], index[next]);
                }
            }

            if (low[node] == index[node])
            {
                var component = new List<string>();
                string member;
                do
                {
                    member = stack.Pop();
                    onStack.Remove(member);
                    component.Add(member);
                }
                while (member != node);
                component.Sort(StringComparer.Ordinal);
                components.Add(component);
            }
        }

        foreach (string node in successors.Keys.Order(StringComparer.Ordinal))
        {
            if (!index.ContainsKey(node))
            {
                Visit(node);
            }
        }

        return components;
    }

    private static string DescribeCycle(DependencyGraph graph, List<string> component)
    {
        var members = new HashSet<string>(component, StringComparer.Ordinal);
        IEnumerable<string> edges = graph.Edges
            .Where(edge => members.Contains(edge.Key.From) && members.Contains(edge.Key.To))
            .OrderBy(static edge => edge.Key.From, StringComparer.Ordinal)
            .ThenBy(static edge => edge.Key.To, StringComparer.Ordinal)
            .Select(static edge => $"  {Short(edge.Key.From)} -> {Short(edge.Key.To)}: {edge.Value}");
        return $"cycle among {string.Join(", ", component.Select(Short))}:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, edges);
    }

    private static string Short(string name) =>
        name == Root ? "(root)" : name[(Root.Length + 1)..];

    private sealed record DependencyGraph(
        IReadOnlySet<string> Namespaces,
        IReadOnlyDictionary<(string From, string To), string> Edges,
        ImmutableArray<string> UnboundErrors);
}
