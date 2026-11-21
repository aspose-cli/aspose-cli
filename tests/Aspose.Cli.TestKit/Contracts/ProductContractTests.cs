using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.CommandLine;
using System.CommandLine.Completions;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.Serialization;
using Json.Schema;
using Xunit;

namespace Aspose.Cli.TestKit;

/// <summary>
/// The count naming rule, checked on the source-generated JSON metadata of every
/// <see cref="JsonSerializerContext"/> in the given assemblies: each context's
/// <c>[JsonSerializable]</c> roots and everything their properties, items and derived types
/// reach. An integer property whose last camelCase word is a plural noun is a count and ends
/// in <c>Count</c> (<c>pageCount</c>, not <c>pages</c> or <c>headerRows</c>); a unit word
/// after a qualifier (<c>sizeBytes</c>, <c>durationMs</c>) names a quantity, not a count, but a
/// bare plural (<c>pages</c>, <c>bytes</c>) is never an integer, so it can only name the
/// objects themselves or a range of them.
/// </summary>
public static class JsonCountNames
{
    /// <summary>Last words that are units of a quantity, used after a qualifier.</summary>
    private static readonly HashSet<string> Units = new(StringComparer.Ordinal) { "Bytes", "Milliseconds", "Ms" };

    /// <summary>Every violation, naming the CLR type, its property and the JSON name.</summary>
    public static IReadOnlyList<string> Violations(IEnumerable<Assembly> assemblies)
    {
        var violations = new List<string>();
        var visited = new HashSet<Type>();
        foreach (Type contextType in assemblies
            .SelectMany(static assembly => assembly.GetTypes())
            .Where(static type => !type.IsAbstract && typeof(JsonSerializerContext).IsAssignableFrom(type))
            .OrderBy(static type => type.FullName, StringComparer.Ordinal))
        {
            var context = (JsonSerializerContext)contextType
                .GetProperty("Default", BindingFlags.Public | BindingFlags.Static)!
                .GetValue(null)!;
            foreach (Type root in contextType.GetCustomAttributesData()
                .Where(static data => data.AttributeType == typeof(JsonSerializableAttribute))
                .Select(static data => (Type)data.ConstructorArguments[0].Value!))
            {
                Walk(context, root, visited, violations);
            }
        }

        return violations;
    }

    private static void Walk(JsonSerializerContext context, Type type, HashSet<Type> visited, List<string> violations)
    {
        if (!visited.Add(type) || context.GetTypeInfo(type) is not { } info)
        {
            return;
        }

        if (info.ElementType is { } element)
        {
            Walk(context, element, visited, violations);
        }

        foreach (JsonDerivedType derived in info.PolymorphismOptions?.DerivedTypes ?? [])
        {
            Walk(context, derived.DerivedType, visited, violations);
        }

        if (info.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        foreach (JsonPropertyInfo property in info.Properties)
        {
            if (IsInteger(property.PropertyType) && IsUncountedPlural(property.Name))
            {
                violations.Add(
                    $"{type.FullName}.{(property.AttributeProvider as MemberInfo)?.Name ?? property.Name}: "
                    + $"integer '{property.Name}' is a count; name it '{Singular(property.Name)}Count'.");
            }

            Walk(context, property.PropertyType, visited, violations);
        }
    }

    private static bool IsInteger(Type type) =>
        (Nullable.GetUnderlyingType(type) ?? type) is var value
        && (value == typeof(int) || value == typeof(long) || value == typeof(short) || value == typeof(byte)
            || value == typeof(uint) || value == typeof(ulong) || value == typeof(ushort) || value == typeof(sbyte));

    /// <summary>Whether a name's last camelCase word is a plural noun other than a qualified unit.</summary>
    private static bool IsUncountedPlural(string name)
    {
        int start = LastWordStart(name);
        string word = name[start..];
        return word.EndsWith('s')
            && !word.EndsWith("ss", StringComparison.Ordinal)
            && !word.EndsWith("us", StringComparison.Ordinal)
            && !word.EndsWith("is", StringComparison.Ordinal)
            && !(start > 0 && Units.Contains(word));
    }

    private static int LastWordStart(string name)
    {
        for (int index = name.Length - 1; index > 0; index--)
        {
            if (char.IsUpper(name[index]))
            {
                return index;
            }
        }

        return 0;
    }

    private static string Singular(string name) =>
        name.EndsWith("ies", StringComparison.Ordinal) ? name[..^3] + "y" : name[..^1];
}

/// <summary>
/// Reusable product-module contract tests. Product test projects inherit this
/// class so catalog and definition invariants are enforced consistently.
/// </summary>
/// <typeparam name="TModule">The product module under test.</typeparam>
public abstract class ProductContractTests<TModule>
    where TModule : IProductModule, new()
{
    /// <summary>Ensures the module produces an equivalent pure definition repeatedly.</summary>
    [Fact]
    public void Define_IsDeterministic()
    {
        var module = new TModule();

        ProductDefinition first = module.Define();
        ProductDefinition second = module.Define();

        Assert.Equal(Fingerprint(first), Fingerprint(second));
    }

    /// <summary>
    /// Ensures command factories are deterministic and contribute exactly the
    /// root owned by their product.
    /// </summary>
    [Fact]
    public void Commands_AreDeterministicAndOwnProductRoot()
    {
        ProductCatalog catalog = ProductCatalog.Build([new TModule()]);
        ProductDefinition product = Assert.Single(catalog.Products);
        Command first = product.CreateCommand(Unavailable);
        Command second = product.CreateCommand(Unavailable);

        Assert.Equal(product.Manifest.Id, first.Name);
        Assert.Equal(CommandFingerprint(first), CommandFingerprint(second));
    }

    /// <summary>Ensures format declarations are canonical and locally unique.</summary>
    [Fact]
    public void Formats_AreCanonicalAndUnique()
    {
        ProductDefinition definition = new TModule().Define();
        Assert.NotEmpty(definition.Formats);
        Assert.Equal(
            definition.Formats.Count,
            definition.Formats
                .Select(static format => format.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());

        foreach (FormatDescriptor format in definition.Formats)
        {
            Assert.Equal(format.Id.ToLowerInvariant(), format.Id);
            Assert.NotEqual(0, (int)format.Uses);
            Assert.NotEmpty(format.Extensions);
            Assert.Equal(
                format.Extensions.Count,
                format.Extensions
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count());
            Assert.All(format.Extensions, static extension =>
            {
                Assert.StartsWith(".", extension, StringComparison.Ordinal);
                Assert.Equal(extension.ToLowerInvariant(), extension);
            });
        }
    }

    /// <summary>
    /// Ensures every declared JSON root has product-local source-generated
    /// metadata and every successful result publishes a schema.
    /// </summary>
    [Fact]
    public void JsonRoots_HaveMetadataAndSchemas()
    {
        ProductDefinition definition = new TModule().Define();
        Assert.NotNull(definition.Json);
        Assembly assembly = typeof(TModule).Assembly;
        Type[] roots = assembly.GetTypes()
            .Where(static type =>
                !type.IsAbstract
                && (typeof(ResultEnvelope).IsAssignableFrom(type)
                    || type.IsDefined(
                        typeof(ProductJsonRootAttribute),
                        inherit: false)))
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(roots);

        foreach (Type root in roots)
        {
            Assert.NotNull(definition.Json!.LocalOptions.GetTypeInfo(root));
        }

        Type[] results = roots
            .Where(static root =>
                typeof(ResultEnvelope).IsAssignableFrom(root))
            .ToArray();
        Assert.NotEmpty(results);

        Assert.Empty(roots.Except(results));

        IReadOnlyList<string> productSchemas = ProductCatalog.Build([new TModule()]).Resources
            .GetProduct(definition.Manifest.Id)
            .SchemaIds;
        foreach (Type result in results)
        {
            string? id = definition.Json!.ResultRecords.FirstOrDefault(record => record.Type == result)?.SchemaId;
            Assert.True(
                id is not null && productSchemas.Contains(ResultSchemaSet.Id(definition.Manifest.Id, id)),
                $"Result {result.FullName} of product '{definition.Manifest.Id}' publishes no schema the product lists.");
        }
    }

    /// <summary>Ensures schema and Skill resources travel with the product.</summary>
    [Fact]
    public void Resources_AreProductOwnedAndConventionBased()
    {
        ProductCatalog catalog = ProductCatalog.Build([new TModule()]);
        ProductDefinition definition = Assert.Single(catalog.Products);
        ProductPackageResources resources =
            catalog.Resources.GetProduct(definition.Manifest.Id);

        Assert.Equal(typeof(TModule).Assembly, resources.ResourceAssembly);
        Assert.Equal(
            "aspose-cli-" + definition.Manifest.Id,
            resources.SkillName);
        Assert.False(string.IsNullOrWhiteSpace(resources.SkillDescription));
        Assert.NotEmpty(resources.SchemaIds);
    }

    /// <summary>Ensures every schema the product publishes parses and states its canonical identity.</summary>
    [Fact]
    public void EveryCurrentSchema_ParsesAndHasCanonicalIdentity()
    {
        ProductDefinition definition = new TModule().Define();
        ProductResourceCatalog resources =
            ProductCatalog.Build([new TModule()]).Resources;
        string[] productSchemas = resources
            .GetProduct(definition.Manifest.Id)
            .SchemaIds
            .ToArray();
        Assert.NotEmpty(productSchemas);

        foreach (string id in productSchemas)
        {
            string text = resources.Read(id);
            _ = ParseSchema(text);
            JsonObject document = JsonNode.Parse(text)!.AsObject();
            Assert.Equal(
                $"https://schemas.aspose.com/aspose-cli/{id}.schema.json",
                document["$id"]?.GetValue<string>());
            Assert.Equal(
                "https://json-schema.org/draft/2020-12/schema",
                document["$schema"]?.GetValue<string>());
        }
    }

    /// <summary>
    /// Names every integer count <c>&lt;noun&gt;Count</c> across the product's source-generated
    /// contracts; see <see cref="JsonCountNames"/>.
    /// </summary>
    [Fact]
    public void IntegerCounts_AreNamedNounCount()
    {
        IReadOnlyList<string> violations = JsonCountNames.Violations([typeof(TModule).Assembly]);
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    private static JsonSchema ParseSchema(string document) =>
        JsonSchema.FromText(
            document,
            SchemaTestRegistry.CreateOptions());

    private static DefinitionFingerprint Fingerprint(ProductDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new DefinitionFingerprint(
            definition.Manifest.Id,
            definition.Manifest.DisplayName,
            definition.Manifest.DisplayOrder,
            definition.SessionType,
            string.Join(
                "\n",
                definition.Json.ResultRecords
                .Select(static record => record.Type)
                .OrderBy(static type => type.FullName, StringComparer.Ordinal)
                .Select(static type => type.AssemblyQualifiedName)),
            string.Join(
                "\n",
                definition.Formats
                    .Select(static format =>
                        string.Join(
                            ":",
                            format.Id,
                            format.Uses,
                            format.InputOrder,
                            format.ConvertOrder,
                            format.RenderOrder,
                            format.OutputExtension,
                            string.Join(',', format.Extensions),
                            string.Join(',', format.Aliases)))
                    .Order(StringComparer.Ordinal)),
            string.Join(
                "\n",
                definition.Diagnostics
                    .Select(static diagnostic => string.Join(
                        ":",
                        diagnostic.Code,
                        diagnostic.Owner,
                        diagnostic.Severity,
                        diagnostic.ExitCode,
                        diagnostic.Category,
                        diagnostic.DetailsSchemaId))
                    .Order(StringComparer.Ordinal)),
            string.Join(
                "|",
                definition.Json.ProductId,
                definition.Json.Resolver.GetType().AssemblyQualifiedName,
                string.Join(
                    ",",
                    definition.Json.Converters
                        .Select(static converter =>
                            converter.GetType().AssemblyQualifiedName)
                        .Order(StringComparer.Ordinal))),
            FactoryFingerprint(definition, "CommandFactory"),
            FactoryFingerprint(definition, "BindingFactory"));
    }

    private static string? FactoryFingerprint(
        ProductDefinition definition,
        string propertyName)
    {
        object? value = typeof(ProductDefinition)
            .GetProperty(
                propertyName,
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic)
            ?.GetValue(definition);
        return value is Delegate factory
            ? $"{factory.Method.DeclaringType?.AssemblyQualifiedName}:"
                + $"{factory.Method.Name}:{factory.Target?.GetType().AssemblyQualifiedName}"
            : value?.GetType().AssemblyQualifiedName;
    }

    private static string CommandFingerprint(Command root)
    {
        var lines = new List<string>();
        Visit(root, string.Empty, lines);
        return string.Join("\n", lines);
    }

    private static void Visit(
        Command command,
        string parent,
        ICollection<string> lines)
    {
        string path = parent.Length == 0
            ? command.Name
            : parent + " " + command.Name;
        lines.Add(string.Join(
            "|",
            "command",
            path,
            string.Join(",", command.Aliases.Order(StringComparer.Ordinal)),
            command.Hidden,
            command.Description));
        foreach (Option option in command.Options.OrderBy(
            static option => option.Name,
            StringComparer.Ordinal))
        {
            lines.Add(string.Join(
                "|",
                "option",
                path,
                option.Name,
                string.Join(",", option.Aliases.Order(StringComparer.Ordinal)),
                option.ValueType.AssemblyQualifiedName,
                option.Arity.MinimumNumberOfValues,
                option.Arity.MaximumNumberOfValues,
                option.Required,
                option.Recursive,
                option.HasDefaultValue,
                CompletionFingerprint(option)));
        }
        foreach (Argument argument in command.Arguments)
        {
            lines.Add(string.Join(
                "|",
                "argument",
                path,
                argument.Name,
                argument.ValueType.AssemblyQualifiedName,
                argument.Arity.MinimumNumberOfValues,
                argument.Arity.MaximumNumberOfValues,
                argument.HasDefaultValue,
                CompletionFingerprint(argument)));
        }
        foreach (Command child in command.Subcommands)
        {
            Visit(child, path, lines);
        }
    }

    private static string CompletionFingerprint(Symbol symbol)
    {
        try
        {
            return string.Join(
                ",",
                symbol.GetCompletions(CompletionContext.Empty)
                    .Select(static item => item.InsertText)
                    .Order(StringComparer.Ordinal));
        }
        catch (Exception)
        {
            return "<context-dependent>";
        }
    }

    private sealed record DefinitionFingerprint(
        string Id,
        string DisplayName,
        int DisplayOrder,
        Type SessionType,
        string OutputTypes,
        string Formats,
        string Diagnostics,
        string? Json,
        string? CommandFactory,
        string? BindingFactory);

    // Contract tests build command trees; they never run a command.
    private static int Unavailable(ParseResult parse, Func<ProductCommandScope, ResultEnvelope> run) =>
        throw ExecutionUnavailable();

    private static InvalidOperationException ExecutionUnavailable() =>
        new("Product contract tests cannot execute commands.");
}
