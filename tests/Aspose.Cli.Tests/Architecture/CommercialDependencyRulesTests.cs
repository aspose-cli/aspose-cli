using System.Reflection;

namespace Aspose.Cli.Tests;

/// <summary>Enforces product and launcher boundaries for the Commercial distribution.</summary>
public sealed class CommercialDependencyRulesTests
{
    private static readonly Assembly CommercialLauncher = typeof(Program).Assembly;

    private static readonly IReadOnlyList<Assembly> Products =
        CompiledProductCatalog.Instance.Resources.Products
            .Select(static product => product.ResourceAssembly)
            .Distinct()
            .ToArray();

    [Fact]
    public void EveryProduct_DependsOnSdkAndNoOtherProduct()
    {
        foreach (Assembly product in Products)
        {
            string[] references = ReferenceNames(product);
            Assert.Contains("Aspose.Cli.Sdk", references);
            Assert.DoesNotContain(
                references,
                static reference => reference.StartsWith(
                    "Aspose.Cli.Product.",
                    StringComparison.Ordinal));
            Assert.DoesNotContain("Aspose.Cli.Contracts", references);
            Assert.DoesNotContain("Aspose.Cli.Core", references);
            Assert.DoesNotContain(
                references,
                static reference => reference.StartsWith(
                    "Aspose.Cli.Engines.",
                    StringComparison.Ordinal));
            Assert.Contains(
                references,
                static reference => reference.StartsWith(
                    "Aspose.",
                    StringComparison.Ordinal)
                    && !reference.StartsWith(
                        "Aspose.Cli.",
                        StringComparison.Ordinal));
        }
    }

    [Fact]
    public void CommercialLauncher_ComposesHostSdkAndActiveProducts()
    {
        string[] references = ReferenceNames(CommercialLauncher);
        string[] productReferences = references
            .Where(static reference => reference.StartsWith(
                "Aspose.Cli.Product.",
                StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] expected = Products
            .Select(static assembly => assembly.GetName().Name!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, productReferences);
        Assert.Contains("Aspose.Cli.Host", references);
        Assert.Contains("Aspose.Cli.Sdk", references);
        Assert.DoesNotContain("Aspose.Cli.Contracts", references);
        Assert.DoesNotContain("Aspose.Cli.Core", references);
        Assert.DoesNotContain(
            references,
            static reference => reference.StartsWith(
                "Aspose.Cli.Engines.",
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            references,
            static reference => reference.StartsWith(
                "Aspose.",
                StringComparison.Ordinal)
                && !reference.StartsWith(
                    "Aspose.Cli.",
                    StringComparison.Ordinal));
    }

    [Fact]
    public void ProductPublicContracts_DoNotExposeAsposeSdkTypes()
    {
        var violations = new List<string>();
        foreach (Assembly product in Products)
        {
            foreach (Type type in product.GetExportedTypes())
            {
                InspectType(type, type.FullName ?? type.Name, violations);
                foreach (PropertyInfo property in type.GetProperties(
                    BindingFlags.Public
                    | BindingFlags.Instance
                    | BindingFlags.Static
                    | BindingFlags.DeclaredOnly))
                {
                    InspectType(
                        property.PropertyType,
                        $"{type.FullName}.{property.Name}",
                        violations);
                }

                foreach (MethodInfo method in type.GetMethods(
                    BindingFlags.Public
                    | BindingFlags.Instance
                    | BindingFlags.Static
                    | BindingFlags.DeclaredOnly))
                {
                    InspectType(
                        method.ReturnType,
                        $"{type.FullName}.{method.Name} return",
                        violations);
                    foreach (ParameterInfo parameter in method.GetParameters())
                    {
                        InspectType(
                            parameter.ParameterType,
                            $"{type.FullName}.{method.Name}({parameter.Name})",
                            violations);
                    }
                    foreach (Type genericArgument in method.GetGenericArguments())
                    {
                        InspectType(
                            genericArgument,
                            $"{type.FullName}.{method.Name}<{genericArgument.Name}>",
                            violations);
                    }
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Aspose SDK types leaked through product public APIs:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    private static string[] ReferenceNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(static reference => reference.Name!)
            .ToArray();

    private static void InspectType(
        Type type,
        string location,
        ICollection<string> violations) =>
        InspectType(type, location, violations, new HashSet<Type>());

    private static void InspectType(
        Type type,
        string location,
        ICollection<string> violations,
        ISet<Type> visited)
    {
        Type leaf = type;
        while (leaf.HasElementType)
        {
            leaf = leaf.GetElementType()!;
        }
        if (!visited.Add(leaf))
        {
            return;
        }

        string? assemblyName = leaf.Assembly.GetName().Name;
        if (assemblyName?.StartsWith("Aspose.", StringComparison.Ordinal) == true
            && !assemblyName.StartsWith(
                "Aspose.Cli.",
                StringComparison.Ordinal))
        {
            violations.Add($"{location}: {leaf.FullName}");
        }

        if (leaf.IsGenericType)
        {
            foreach (Type argument in leaf.GetGenericArguments())
            {
                InspectType(argument, location, violations, visited);
            }
        }
        if (leaf.IsGenericParameter)
        {
            foreach (Type constraint in leaf.GetGenericParameterConstraints())
            {
                InspectType(constraint, location, violations, visited);
            }
        }
    }
}
