using System.Reflection;
using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Analyzers;
using Aspose.Cli.Sdk.Operations;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// The analyzers recognize SDK types by the names in one table; a rename in the SDK fails here
/// instead of quietly switching off the rule that names the type.
/// </summary>
public sealed class SdkMetadataNamesTests
{
    private const string TableName = "Aspose.Cli.Sdk.Analyzers.SdkMetadataNames";

    [Fact]
    public void EveryName_ResolvesInTheSdk()
    {
        Assembly sdk = typeof(OperationCatalog<>).Assembly;
        string[] unresolved = [.. Unresolved(Table(), sdk)];

        Assert.Equal("Aspose.Cli.Sdk", sdk.GetName().Name);
        Assert.Equal(sdk.GetName().Name, Assert.Single(Names(Table()), static name => name.Field == "SdkAssembly").Value);
        Assert.True(Names(Table()).Length > 60, "The table must list the SDK types the analyzers recognize.");
        Assert.True(unresolved.Length == 0, "Names the SDK does not declare: " + string.Join(", ", unresolved));
    }

    [Fact]
    public void EveryJsonName_ResolvesInSystemTextJson()
    {
        Type json = Assert.Single(Table().GetNestedTypes(BindingFlags.NonPublic), static type => type.Name == "Json");
        string[] unresolved = [.. Unresolved(json, typeof(JsonSerializerContext).Assembly, typeof(System.Text.Json.Nodes.JsonNode).Assembly)];

        Assert.True(unresolved.Length == 0, "Names System.Text.Json does not declare: " + string.Join(", ", unresolved));
    }

    [Fact]
    public void ContractSymbols_AreNamesOfTheTable()
    {
        string[] symbols = Assert.IsType<string[]>(Table().GetField("ContractSymbols", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null));
        string[] names = [.. Names(Table()).Select(static name => name.Value)];

        Assert.NotEmpty(symbols);
        Assert.All(symbols, symbol => Assert.Contains(symbol, names));
    }

    private static Type Table() =>
        typeof(OperationContractGenerator).Assembly.GetType(TableName, throwOnError: true)!;

    private static (string Field, string Value)[] Names(Type table) =>
        [.. table.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(static field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(static field => (field.Name, (string)field.GetRawConstantValue()!))];

    private static IEnumerable<string> Unresolved(Type table, params Assembly[] assemblies)
    {
        HashSet<string?> namespaces = [.. assemblies.SelectMany(static assembly => assembly.GetTypes()).Select(static type => type.Namespace)];
        foreach ((string field, string value) in Names(table))
        {
            bool resolved = field == "SdkAssembly"
                || (field.EndsWith("Namespace", StringComparison.Ordinal)
                    ? namespaces.Contains(value)
                    : assemblies.Any(assembly => assembly.GetType(value) is not null));
            if (!resolved)
            {
                yield return $"{field} = {value}";
            }
        }
    }
}
