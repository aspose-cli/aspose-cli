using System.Reflection;
using System.Runtime.CompilerServices;

namespace Aspose.Cli.Tests;

/// <summary>
/// A product holds its module definition at its root and everything else in its three layers:
/// Contracts, Commands and Engine. Code the build generates for it lives under
/// <c>Aspose.Cli.Generated</c>. A type in any other namespace is a layer the analyzers do not
/// know, so its dependencies go unchecked.
/// </summary>
public sealed class CommandShapeTests
{
    private const string ProductPrefix = "Aspose.Cli.Product.";
    private const string Generated = "Aspose.Cli.Generated";
    private static readonly string[] Layers = ["Contracts", "Commands", "Engine"];

    private static readonly IReadOnlyList<Assembly> Products =
        CompiledProductCatalog.Instance.Resources.Products
            .Select(static product => product.ResourceAssembly)
            .Distinct()
            .ToArray();

    [Fact]
    public void ProductTypes_LiveAtTheProductRootInALayerOrInGeneratedCode()
    {
        Assert.NotEmpty(Products);
        var found = new List<string>();

        foreach (Assembly product in Products)
        {
            string name = product.GetName().Name!;
            Assert.StartsWith(ProductPrefix, name, StringComparison.Ordinal);
            foreach (Type type in TypesOf(product).Where(static type => !type.IsNested && !IsCompilerEmitted(type)))
            {
                if (!Allowed(name, type.Namespace))
                {
                    found.Add($"{name}: {type.FullName}");
                }
            }
        }

        Assert.True(
            found.Count == 0,
            $"Product types belong at the product root, in {string.Join(", ", Layers)}, or in {Generated}:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, found.Order(StringComparer.Ordinal)));
    }

    private static bool Allowed(string root, string? space) =>
        space is not null
        && (space == root
            || Within(space, Generated)
            || Layers.Any(layer => Within(space, root + "." + layer)));

    private static bool Within(string space, string parent) =>
        space == parent || space.StartsWith(parent + ".", StringComparison.Ordinal);

    /// <summary>
    /// Types the C# compiler adds on its own, such as the embedded nullable attributes, and
    /// file-local types, such as the regular expression generator's, whose mangled names begin
    /// with '&lt;' and which only the file that declares them can use.
    /// </summary>
    private static bool IsCompilerEmitted(Type type) =>
        type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
        || type.Name.StartsWith('<');

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
