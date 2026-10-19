using System.Collections.Immutable;
using Aspose.Cli.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>One reference from a type to a type of another namespace of the same assembly.</summary>
/// <param name="Owner">The outermost type that holds the reference.</param>
/// <param name="Target">The outermost type referenced.</param>
/// <param name="Location">The repository-relative file and line of the first such reference.</param>
internal sealed record NamespaceReference(INamedTypeSymbol Owner, INamedTypeSymbol Target, string Location)
{
    public override string ToString() => $"{Owner.Name} uses {Target.Name} ({Location})";
}

/// <summary>
/// The namespace dependency graph of one assembly's source, read with the Roslyn semantic
/// model: every name that binds to a type of the assembly counts, written through a using
/// directive, a global using or a qualified name alike.
/// </summary>
internal sealed record NamespaceGraph(
    string Root,
    IReadOnlySet<string> Namespaces,
    IReadOnlyDictionary<(string From, string To), IReadOnlyList<NamespaceReference>> Edges,
    ImmutableArray<string> UnboundErrors)
{
    /// <summary>The C# files of one source project, without its build output.</summary>
    public static IEnumerable<string> SourceFiles(string project)
    {
        string separator = Path.DirectorySeparatorChar.ToString();
        return Directory
            .EnumerateFiles(project, "*.cs", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(project, path).Split(separator).Any(
                static part => part is "bin" or "obj"));
    }

    /// <summary>
    /// Compiles <paramref name="files"/> with the SDK project's implicit usings and builds the
    /// graph of the namespaces under <paramref name="root"/>.
    /// </summary>
    public static NamespaceGraph Build(string root, IEnumerable<string> files, IEnumerable<MetadataReference> references)
    {
        CSharpParseOptions options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        List<SyntaxTree> trees = files
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
            root,
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var namespaces = new HashSet<string>(StringComparer.Ordinal);
        var edges = new Dictionary<(string From, string To), List<NamespaceReference>>();
        var seen = new HashSet<(INamedTypeSymbol, INamedTypeSymbol)>(new SymbolPairComparer());
        foreach (SyntaxTree tree in trees)
        {
            SemanticModel model = compilation.GetSemanticModel(tree);
            SyntaxNode syntax = tree.GetRoot();
            foreach (BaseTypeDeclarationSyntax declaration in syntax.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(declaration) is { } declared)
                {
                    namespaces.Add(declared.ContainingNamespace.ToDisplayString());
                }
            }

            foreach (SimpleNameSyntax name in syntax.DescendantNodes().OfType<SimpleNameSyntax>())
            {
                if (ReferencedType(model, name) is not { } target
                    || !SymbolEqualityComparer.Default.Equals(target.ContainingAssembly, compilation.Assembly)
                    || OwnerOf(model, name) is not { } owner)
                {
                    continue;
                }

                string from = owner.ContainingNamespace.ToDisplayString();
                string to = target.ContainingNamespace.ToDisplayString();
                if (from == to || !from.StartsWith(root, StringComparison.Ordinal) || !seen.Add((owner, target)))
                {
                    continue;
                }

                int line = name.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                string location = $"{Path.GetRelativePath(RepositoryPaths.Root, tree.FilePath).Replace('\\', '/')}:{line}";
                if (!edges.TryGetValue((from, to), out List<NamespaceReference>? list))
                {
                    edges[(from, to)] = list = [];
                }

                list.Add(new NamespaceReference(owner, target, location));
            }
        }

        ImmutableArray<string> unbound = [.. compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error && !GeneratedMemberError(compilation, diagnostic))
            .Select(static diagnostic => diagnostic.ToString())
            .Take(20)];
        return new NamespaceGraph(
            root,
            namespaces,
            edges.ToDictionary(static edge => edge.Key, static edge => (IReadOnlyList<NamespaceReference>)edge.Value),
            unbound);
    }

    /// <summary>The graph without the references <paramref name="excluded"/> picks.</summary>
    public NamespaceGraph Without(Func<NamespaceReference, bool> excluded) =>
        this with
        {
            Edges = Edges
                .Select(edge => (edge.Key, References: (IReadOnlyList<NamespaceReference>)edge.Value.Where(reference => !excluded(reference)).ToList()))
                .Where(static edge => edge.References.Count > 0)
                .ToDictionary(static edge => edge.Key, static edge => edge.References),
        };

    /// <summary>The namespace without the root prefix, or "(root)" for the root itself.</summary>
    public string Short(string name) =>
        name == Root ? "(root)" : name[(Root.Length + 1)..];

    /// <summary>The strongly connected components, each sorted, in a stable order.</summary>
    public List<List<string>> StronglyConnectedComponents()
    {
        // Tarjan's algorithm over the namespaces in ordinal order, so the report is stable.
        Dictionary<string, string[]> successors = Namespaces
            .ToDictionary(
                static name => name,
                name => Edges.Keys.Where(edge => edge.From == name).Select(static edge => edge.To)
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

    /// <summary>The edges among the namespaces of one cycle, each with one example reference.</summary>
    public string DescribeCycle(List<string> component)
    {
        var members = new HashSet<string>(component, StringComparer.Ordinal);
        IEnumerable<string> edges = Edges
            .Where(edge => members.Contains(edge.Key.From) && members.Contains(edge.Key.To))
            .OrderBy(static edge => edge.Key.From, StringComparer.Ordinal)
            .ThenBy(static edge => edge.Key.To, StringComparer.Ordinal)
            .Select(edge => $"  {Short(edge.Key.From)} -> {Short(edge.Key.To)}: {edge.Value[0]}");
        return $"cycle among {string.Join(", ", component.Select(Short))}:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, edges);
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

    // The type whose declaration holds the name. Its attributes, base list, constraints and a
    // delegate's signature belong to it as much as its members do, though the semantic
    // model encloses them in the namespace.
    private static INamedTypeSymbol? OwnerOf(SemanticModel model, SyntaxNode name) =>
        name.Ancestors().FirstOrDefault(static node => node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax)
            is { } declaration
            && model.GetDeclaredSymbol(declaration) is INamedTypeSymbol type
                ? Outermost(type)
                : null;

    private static INamedTypeSymbol Outermost(INamedTypeSymbol type)
    {
        while (type.ContainingType is { } containing)
        {
            type = containing;
        }

        return type;
    }

    private sealed class SymbolPairComparer : IEqualityComparer<(INamedTypeSymbol, INamedTypeSymbol)>
    {
        public bool Equals((INamedTypeSymbol, INamedTypeSymbol) x, (INamedTypeSymbol, INamedTypeSymbol) y) =>
            SymbolEqualityComparer.Default.Equals(x.Item1, y.Item1)
            && SymbolEqualityComparer.Default.Equals(x.Item2, y.Item2);

        public int GetHashCode((INamedTypeSymbol, INamedTypeSymbol) obj) =>
            HashCode.Combine(
                SymbolEqualityComparer.Default.GetHashCode(obj.Item1),
                SymbolEqualityComparer.Default.GetHashCode(obj.Item2));
    }
}
