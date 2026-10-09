using System.Reflection;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Tests;

/// <summary>
/// A command exists for users only once its product menu pairs it with a handler, and a
/// forgotten menu line compiles and fails nothing else. So every command a product declares
/// must appear in the command tree the CLI builds.
/// </summary>
/// <remarks>
/// The rule, independent of how the menu is written: a command declaration is a static
/// <c>Create</c> method of a top-level static class in the product's <c>Commands</c> namespace
/// (or a namespace below it). It takes no parameters, and the definition it returns names its
/// command through a public string <c>Name</c> property. Every declared name appears in the
/// product's part of the built tree, read from <c>capabilities &lt;product&gt;</c>, at least as
/// often as it is declared, so a name two groups share is still counted per declaration.
/// Helper classes in the namespace declare no <c>Create</c> method.
/// </remarks>
public sealed class ProductMenuCompletenessTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void EveryDeclaredCommand_IsInTheBuiltCommandTree()
    {
        ProductCatalog catalog = CompiledProductCatalog.Instance;
        Assert.NotEmpty(catalog.Products);
        var problems = new List<string>();

        foreach (ProductDefinition product in catalog.Products)
        {
            string id = product.Manifest.Id;
            Assembly assembly = catalog.Resources.GetProduct(id).ResourceAssembly;
            string commands = assembly.GetName().Name + ".Commands";
            Type[] declarations = TypesOf(assembly)
                .Where(type => IsInNamespace(type, commands) && IsTopLevelStaticClass(type))
                .Where(static type => CreateMethods(type).Length > 0)
                .OrderBy(static type => type.FullName, StringComparer.Ordinal)
                .ToArray();
            if (declarations.Length == 0)
            {
                problems.Add($"{id}: no command declaration (a static Create method) in {commands}");
                continue;
            }

            Dictionary<string, int> tree = TreeNodeNames(id);
            var declared = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Type declaration in declarations)
            {
                if (DeclaredName(declaration, problems) is { } name)
                {
                    declared[name] = declared.GetValueOrDefault(name) + 1;
                }
            }

            foreach ((string name, int count) in declared.OrderBy(static entry => entry.Key, StringComparer.Ordinal))
            {
                int built = tree.GetValueOrDefault(name);
                if (built < count)
                {
                    problems.Add(
                        $"{id}: '{name}' is declared {count} time(s) in {commands} but appears {built} time(s) "
                        + "in the built command tree; add its line to the product menu");
                }
            }
        }

        Assert.True(
            problems.Count == 0,
            "Every command a product declares must be on its menu:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, problems));
    }

    /// <summary>The command name a declaration produces, or null after recording why it has none.</summary>
    private static string? DeclaredName(Type declaration, List<string> problems)
    {
        MethodInfo[] creates = CreateMethods(declaration);
        MethodInfo[] parameterized = creates.Where(static method => method.GetParameters().Length > 0).ToArray();
        if (parameterized.Length > 0)
        {
            problems.AddRange(parameterized.Select(method =>
                $"{declaration.FullName}.Create({string.Join(", ", method.GetParameters().Select(static parameter => parameter.ParameterType.Name))}) "
                + "takes parameters; a command declaration is a parameterless Create() that the menu pairs with its handler"));
            return null;
        }
        MethodInfo create = Assert.Single(creates);
        if (create.ContainsGenericParameters)
        {
            problems.Add($"{declaration.FullName}.Create() is generic; a command declaration is a plain Create()");
            return null;
        }

        object? definition = create.Invoke(null, null);
        if (definition is null)
        {
            problems.Add($"{declaration.FullName}.Create() returned null");
            return null;
        }
        PropertyInfo? property = definition.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
        if (property?.PropertyType != typeof(string) || property.GetValue(definition) is not string name
            || string.IsNullOrWhiteSpace(name))
        {
            problems.Add(
                $"{declaration.FullName}.Create() returns {definition.GetType().FullName}, "
                + "which names no command through a public string Name property");
            return null;
        }
        return name;
    }

    /// <summary>How often each command name occurs below the product's group in the built tree.</summary>
    private Dictionary<string, int> TreeNodeNames(string product)
    {
        CliResult result = _workspace.Run("capabilities", product, "--output", "json");
        Assert.True(result.ExitCode == 0, $"capabilities {product} exited {result.ExitCode}: {result.StdErr}");
        JsonNode entry = Assert.Single(JsonNode.Parse(result.StdOut)!["products"]!.AsArray())!;
        var names = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (JsonNode? command in entry["commands"]!.AsArray())
        {
            string[] path = command!["path"]!.GetValue<string>().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (path.Length >= 2)
            {
                names[path[^1]] = names.GetValueOrDefault(path[^1]) + 1;
            }
        }
        Assert.NotEmpty(names);
        return names;
    }

    private static MethodInfo[] CreateMethods(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(static method => string.Equals(method.Name, "Create", StringComparison.Ordinal))
            .ToArray();

    private static bool IsInNamespace(Type type, string space) =>
        type.Namespace is { } name
        && (string.Equals(name, space, StringComparison.Ordinal)
            || name.StartsWith(space + ".", StringComparison.Ordinal));

    private static bool IsTopLevelStaticClass(Type type) =>
        type is { IsClass: true, IsAbstract: true, IsSealed: true, IsNested: false }
        && !type.Name.Contains('<', StringComparison.Ordinal);

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
