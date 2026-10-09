using System.Reflection;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Tests;

/// <summary>
/// A command is a request and result contract, a command definition and a static engine handler,
/// paired by one line of its product's menu. The shapes that wrapped that pairing are gone for
/// good: a typed command host, a product port interface with its <c>Ports</c> namespace, an
/// engine facade that only forwards to services, and the separate <c>.Commands(...)</c> and
/// <c>.Output&lt;T&gt;(...)</c> registrations.
/// </summary>
public sealed class CommandShapeTests
{
    private static readonly Assembly Sdk = typeof(ProductDefinition).Assembly;

    private static readonly IReadOnlyList<Assembly> Products =
        CompiledProductCatalog.Instance.Resources.Products
            .Select(static product => product.ResourceAssembly)
            .Distinct()
            .ToArray();

    [Fact]
    public void Sdk_HasNoCommandHostOrPortShapedRegistration()
    {
        Type[] types = TypesOf(Sdk);
        var found = new List<string>();

        foreach (string retired in new[] { "IProductCommandHost`1", "ProductCommandContext`1" })
        {
            found.AddRange(types
                .Where(type => string.Equals(type.Name, retired, StringComparison.Ordinal))
                .Select(static type => $"type {type.FullName}"));
        }

        found.AddRange(types
            .Where(static type => string.Equals(type.Name, "StandardCommand", StringComparison.Ordinal))
            .SelectMany(static type => PublicMethods(type, "Create"))
            .Select(static method => $"public {method.DeclaringType!.FullName}.{method.Name}"));

        found.AddRange(types
            .Where(static type => type.Name.StartsWith("ProductDefinitionBuilder", StringComparison.Ordinal))
            .SelectMany(static type => PublicMethods(type, "Commands").Concat(PublicMethods(type, "Output")))
            .Select(static method => $"public {method.DeclaringType!.FullName}.{method.Name}"));

        found.AddRange(types
            .SelectMany(static type => PublicMethods(type, "OpenEngine"))
            .Select(static method => $"public {method.DeclaringType!.FullName}.{method.Name}"));

        Assert.True(
            found.Count == 0,
            "The SDK still exposes the command-host shape that the product menu replaced:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, found.Order(StringComparer.Ordinal)));
    }

    [Fact]
    public void Products_DeclareNoPortsNamespaceEngineInterfaceOrEngineFacade()
    {
        Assert.NotEmpty(Products);
        var found = new List<string>();

        foreach (Assembly product in Products)
        {
            string name = ProductName(product);
            foreach (Type type in TypesOf(product).Where(static type => !type.IsNested))
            {
                string? space = type.Namespace;
                if (space is not null
                    && (space.EndsWith(".Ports", StringComparison.Ordinal)
                        || space.Contains(".Ports.", StringComparison.Ordinal)))
                {
                    found.Add($"{product.GetName().Name}: {type.FullName} is in a Ports namespace");
                }
                if (type.IsInterface && string.Equals(type.Name, $"I{name}Engine", StringComparison.Ordinal))
                {
                    found.Add($"{product.GetName().Name}: {type.FullName} is a wide engine port");
                }
                if (type.IsClass && string.Equals(type.Name, $"{name}Engine", StringComparison.Ordinal))
                {
                    found.Add($"{product.GetName().Name}: {type.FullName} is an engine facade");
                }
            }
        }

        Assert.True(
            found.Count == 0,
            "Commands reach their engine through a static handler paired in the menu, not through a port or facade:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, found.Order(StringComparer.Ordinal)));
    }

    [Fact]
    public void ProductSources_RegisterNoCommandFactoryOrOutputList()
    {
        string[] products = ProductSourceDirectories();
        Assert.NotEmpty(products);
        var found = new List<string>();

        foreach (string directory in products)
        {
            if (Directory.Exists(Path.Combine(directory, "Ports")))
            {
                found.Add($"{Relative(Path.Combine(directory, "Ports"))}: a Ports directory");
            }
            foreach (string file in SourceFiles(directory))
            {
                string[] lines = File.ReadAllLines(file);
                for (int index = 0; index < lines.Length; index++)
                {
                    foreach (string retired in new[] { ".Output<", ".Commands(" })
                    {
                        if (lines[index].Contains(retired, StringComparison.Ordinal))
                        {
                            found.Add($"{Relative(file)}:{index + 1}: {retired}");
                        }
                    }
                }
            }
        }

        Assert.True(
            found.Count == 0,
            "Each command and its table rendering are paired in the product menu; "
            + "these sources still register a command factory, an output list or a port:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, found));
    }

    [Fact]
    public void ContractAnalyzer_KnowsNoPortsLayer()
    {
        string analyzers = Path.Combine(RepositoryPaths.Root, "src", "Aspose.Cli.Sdk.Analyzers");
        Assert.True(Directory.Exists(analyzers), analyzers);

        string[] found = SourceFiles(analyzers)
            .SelectMany(static file => File.ReadAllLines(file)
                .Select((line, index) => (file, line, index)))
            .Where(static entry => entry.line.Contains("\"Ports\"", StringComparison.Ordinal))
            .Select(static entry => $"{Relative(entry.file)}:{entry.index + 1}")
            .ToArray();

        Assert.True(
            found.Length == 0,
            "The product layer analyzer still names a Ports layer:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, found));
    }

    private static string ProductName(Assembly product)
    {
        const string prefix = "Aspose.Cli.Product.";
        string name = product.GetName().Name!;
        Assert.StartsWith(prefix, name, StringComparison.Ordinal);
        return name[prefix.Length..];
    }

    private static string[] ProductSourceDirectories() =>
        Directory.GetDirectories(Path.Combine(RepositoryPaths.Root, "src"), "Aspose.Cli.Product.*")
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static IEnumerable<string> SourceFiles(string directory) =>
        Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(file =>
            {
                string relative = Path.GetRelativePath(directory, file);
                string first = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
                return first is not ("bin" or "obj");
            })
            .Order(StringComparer.Ordinal);

    private static string Relative(string path) =>
        Path.GetRelativePath(RepositoryPaths.Root, path).Replace('\\', '/');

    /// <summary>The methods of this name that the SDK exposes to products: public, on a public type.</summary>
    private static IEnumerable<MethodInfo> PublicMethods(Type type, string name) =>
        type.IsVisible
            ? type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => string.Equals(method.Name, name, StringComparison.Ordinal))
            : [];

    private static Type[] TypesOf(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>().ToArray();
        }
    }
}
