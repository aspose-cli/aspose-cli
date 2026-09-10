using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.CommandLine;
using System.CommandLine.Completions;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Preview;
using Aspose.Cli.Sdk.Serialization;
using Json.Schema;
using Xunit;

namespace Aspose.Cli.TestKit;

/// <summary>A product-owned input value and the schema that describes it.</summary>
public sealed record ProductSchemaSample(string Schema, object Value);

/// <summary>
/// Reusable product-module contract tests. Product test projects inherit this
/// class so catalog and definition invariants are enforced consistently.
/// </summary>
/// <typeparam name="TModule">The product module under test.</typeparam>
public abstract class ProductContractTests<TModule>
    where TModule : IProductModule, new()
{
    /// <summary>Product-owned canonical result samples.</summary>
    protected abstract IReadOnlyList<ResultEnvelope> CanonicalResults { get; }

    /// <summary>Product-owned canonical input samples.</summary>
    protected abstract IReadOnlyList<ProductSchemaSample> CanonicalInputs { get; }

    /// <summary>Ensures the module produces an equivalent pure definition repeatedly.</summary>
    [Fact]
    public void Define_IsDeterministic()
    {
        var module = new TModule();

        ProductDefinition first = module.Define();
        ProductDefinition second = module.Define();

        Assert.Equal(Fingerprint(first), Fingerprint(second));
        ProductCatalog firstCatalog = ProductCatalog.Build([new TModule()]);
        ProductCatalog secondCatalog = ProductCatalog.Build([new TModule()]);
        Assert.Equal(
            JsonSerializer.Serialize(
                firstCatalog.GetCapabilities(
                    firstCatalog.Get(first.Manifest.Id))),
            JsonSerializer.Serialize(
                secondCatalog.GetCapabilities(
                    secondCatalog.Get(second.Manifest.Id))));
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
        var host = new ContractCommandHostFactory(catalog);

        Command first = product.CreateCommand(host);
        Command second = product.CreateCommand(host);

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
    /// metadata and every successful result is rendered by its product.
    /// </summary>
    [Fact]
    public void JsonRoots_HaveMetadataRendererAndSchemas()
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
        Assert.Equal(
            results.OrderBy(static type => type.FullName, StringComparer.Ordinal),
            definition.Outputs
                .Select(static output => output.ResultType)
                .OrderBy(static type => type.FullName, StringComparer.Ordinal));

        Type[] inputs = roots
            .Where(static root =>
                !typeof(ResultEnvelope).IsAssignableFrom(root))
            .ToArray();
        Assert.Equal(
            inputs.OrderBy(static type => type.FullName, StringComparer.Ordinal),
            CanonicalInputs
                .Select(static sample => sample.Value.GetType())
                .Distinct()
                .OrderBy(static type => type.FullName, StringComparer.Ordinal));

        ProductResourceCatalog resources =
            ProductCatalog.Build([new TModule()]).Resources;
        string[] productSchemas = resources
            .GetProduct(definition.Manifest.Id)
            .SchemaIds
            .ToArray();
        Assert.True(
            productSchemas.Length >= results.Length,
            $"Product '{definition.Manifest.Id}' has {results.Length} result roots but only {productSchemas.Length} schemas.");
    }

    /// <summary>Ensures schema, preview, and Skill resources travel with the product.</summary>
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
        Assert.Contains("Preview/live-client.js", resources.ResourceNames);
        Assert.Contains("Preview/shell.css", resources.ResourceNames);
        Assert.NotEmpty(resources.SchemaIds);
    }

    /// <summary>
    /// Ensures product-owned samples cover every exact result renderer and do
    /// not claim another product's result family.
    /// </summary>
    [Fact]
    public void CanonicalResults_CoverEveryProductResult()
    {
        ProductDefinition definition = new TModule().Define();
        Type[] expected = definition.Outputs
            .Select(static output => output.ResultType)
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .ToArray();
        Type[] actual = CanonicalResults
            .Select(static sample => sample.GetType())
            .Distinct()
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(CanonicalResults);
        Assert.Equal(expected, actual);
    }

    /// <summary>Prevents product edit results from drifting away from the shared operation wire contract.</summary>
    [Fact]
    public void PartialOutcomes_UseSharedBoundedOperationContract()
    {
        ResultEnvelope[] outcomes = CanonicalResults
            .Where(static result => result is IPartialOutcome)
            .ToArray();
        Assert.Single(outcomes);

        PropertyInfo? applied = outcomes[0].GetType().GetProperty("Applied");
        Assert.NotNull(applied);
        Assert.Equal(
            typeof(IReadOnlyList<BoundedOperationOutcome>),
            applied!.PropertyType);

        Assert.Equal(
            ["Error", "Id", "Index", "ItemsAffected", "Op", "Status", "Targets"],
            typeof(BoundedOperationOutcome)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(static property => property.Name)
                .OrderBy(static name => name, StringComparer.Ordinal));
        Assert.Equal(
            ["Code", "Hint", "Message"],
            typeof(OpError)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(static property => property.Name)
                .OrderBy(static name => name, StringComparer.Ordinal));
    }

    /// <summary>Validates every canonical JSON result against its declared schema.</summary>
    [Fact]
    public void CanonicalResults_ConformToCurrentSchemas()
    {
        ProductDefinition definition = new TModule().Define();
        Assert.NotNull(definition.Json);
        ProductResourceCatalog resources =
            ProductCatalog.Build([new TModule()]).Resources;

        foreach (ResultEnvelope sample in CanonicalResults)
        {
            string schemaId = ResourceSchemaId(sample.Schema);
            string schemaText = resources.Read(schemaId);
            JsonSchema schema = ParseSchema(schemaText);
            string json = JsonSerializer.Serialize(
                sample,
                sample.GetType(),
                definition.Json!.LocalOptions);
            using JsonDocument instance = JsonDocument.Parse(json);
            EvaluationResults evaluation = schema.Evaluate(instance.RootElement);
            Assert.True(
                evaluation.IsValid,
                $"{sample.GetType().FullName} does not conform to {schemaId}: "
                    + JsonSerializer.Serialize(evaluation));

            JsonObject missingRequired = JsonNode.Parse(json)!.AsObject();
            Assert.True(missingRequired.Remove("schema"));
            Assert.False(
                schema.Evaluate(
                    JsonDocument.Parse(missingRequired.ToJsonString()).RootElement)
                    .IsValid,
                $"{schemaId} accepted an instance without required 'schema'.");

            JsonObject wrongType = JsonNode.Parse(json)!.AsObject();
            wrongType["schemaVersion"] = "not-an-integer";
            Assert.False(
                schema.Evaluate(
                    JsonDocument.Parse(wrongType.ToJsonString()).RootElement)
                    .IsValid,
                $"{schemaId} accepted an invalid schemaVersion type.");

            JsonObject schemaDocument = JsonNode.Parse(schemaText)!.AsObject();
            if (schemaDocument["additionalProperties"] is JsonValue policy
                && policy.TryGetValue(out bool allowAdditional)
                && !allowAdditional)
            {
                JsonObject additional = JsonNode.Parse(json)!.AsObject();
                additional["__unexpected"] = true;
                Assert.False(
                    schema.Evaluate(
                        JsonDocument.Parse(additional.ToJsonString()).RootElement)
                        .IsValid,
                    $"{schemaId} accepted an undeclared property despite "
                        + "additionalProperties=false.");
            }
        }
    }

    /// <summary>Validates every canonical product input against its declared schema.</summary>
    [Fact]
    public void CanonicalInputs_ConformToCurrentSchemas()
    {
        ProductDefinition definition = new TModule().Define();
        Assert.NotNull(definition.Json);
        ProductResourceCatalog resources =
            ProductCatalog.Build([new TModule()]).Resources;

        Assert.NotEmpty(CanonicalInputs);
        Assert.Equal(
            CanonicalInputs.Count,
            CanonicalInputs
                .Select(static sample => sample.Schema)
                .Distinct(StringComparer.Ordinal)
                .Count());

        foreach (ProductSchemaSample sample in CanonicalInputs)
        {
            Assert.NotNull(sample.Value);
            Assert.NotNull(
                definition.Json!.LocalOptions.GetTypeInfo(sample.Value.GetType()));
            string schemaId = ResourceSchemaId(sample.Schema);
            JsonSchema schema = ParseSchema(resources.Read(schemaId));
            string json = JsonSerializer.Serialize(
                sample.Value,
                sample.Value.GetType(),
                definition.Json.LocalOptions);
            using JsonDocument instance = JsonDocument.Parse(json);
            EvaluationResults evaluation = schema.Evaluate(instance.RootElement);
            Assert.True(
                evaluation.IsValid,
                $"{sample.Value.GetType().FullName} does not conform to {schemaId}: "
                    + JsonSerializer.Serialize(evaluation));
        }
    }

    /// <summary>Ensures all embedded product schemas parse and identify their current resources.</summary>
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
                $"https://schemas.aspose.dev/aspose-cli/{id}.schema.json",
                document["$id"]?.GetValue<string>());
            Assert.Equal(
                "https://json-schema.org/draft/2020-12/schema",
                document["$schema"]?.GetValue<string>());
        }
    }

    /// <summary>
    /// Ensures every manifest operation has one discoverable, self-contained
    /// schema view and no operation is silently omitted from the index.
    /// </summary>
    [Fact]
    public void Operations_HaveExactDiscoverableSchemaViews()
    {
        ProductCatalog catalog = ProductCatalog.Build([new TModule()]);
        ProductDefinition definition = Assert.Single(catalog.Products);

        foreach (IGrouping<string, ProductOperationDescriptor> group in
            definition.Manifest.Operations.GroupBy(
                static operation => operation.InputSchema,
                StringComparer.Ordinal))
        {
            string[] expected = group
                .Select(static operation => operation.Id)
                .Order(StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(expected, catalog.Resources.GetOperations(group.Key));

            foreach (string operationId in expected)
            {
                Assert.True(
                    catalog.Resources.TryReadOperation(
                        group.Key,
                        operationId,
                        out string? selected));
                _ = ParseSchema(selected);
            }

            Assert.False(
                catalog.Resources.TryReadOperation(
                    group.Key,
                    "__unknown_operation__",
                    out _));
        }
    }

    private static string ResourceSchemaId(string schema)
    {
        const string prefix = "https://schemas.aspose.dev/aspose-cli/";
        const string suffix = ".schema.json";
        Assert.StartsWith(prefix, schema, StringComparison.Ordinal);
        Assert.EndsWith(suffix, schema, StringComparison.Ordinal);
        return schema[prefix.Length..^suffix.Length];
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
            definition.Manifest.ContractVersion,
            definition.PortType,
            string.Join(
                "\n",
                definition.Files.DefaultOwnerExtensions.Order(StringComparer.Ordinal)),
            string.Join(
                "\n",
                definition.Files.AcceptedInputExtensions.Order(StringComparer.Ordinal)),
            RecognizerFingerprint(definition.Files.Recognizer),
            string.Join(
                "\n",
                definition.Outputs
                .Select(static output => output.ResultType)
                .OrderBy(static type => type.FullName, StringComparer.Ordinal)
                .Select(static type => type.AssemblyQualifiedName)),
            string.Join(
                "\n",
                definition.Capabilities
                .Select(static capability =>
                    $"{capability.Relation}:{capability.DisplayName}:"
                    + capability.CapabilityType.AssemblyQualifiedName)
                .Order(StringComparer.Ordinal)),
            string.Join(
                "\n",
                definition.Formats
                    .Select(static format =>
                        string.Join(
                            ":",
                            format.Id,
                            format.Uses,
                            format.Ownership,
                            format.InputOrder,
                            format.ConvertOrder,
                            format.RenderOrder,
                            format.OutputExtension,
                            string.Join(',', format.Extensions),
                            string.Join(',', format.Aliases),
                            string.Join(',', format.Operations),
                            RecognizerFingerprint(format.Recognizer)))
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
                        diagnostic.MessageTemplateId,
                        diagnostic.HintTemplateId,
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
            string.Join(
                "|",
                $"{definition.Preview.ProductId}:{definition.Preview.PortType.AssemblyQualifiedName}:{definition.Preview.DefaultView}",
                string.Join(
                    ",",
                    definition.Preview.ViewDefinitions.Select(
                        static view => $"{view.Id}:{view.DisplayName}")),
                string.Join(
                    ",",
                    definition.Preview.PayloadContracts.Select(
                        static contract =>
                            $"{contract.Kind}@{contract.SchemaVersion}:{contract.SchemaId}:{contract.MaxBytes}"))),
            FactoryFingerprint(definition, "CommandFactory"),
            FactoryFingerprint(definition, "BindingFactory"));
    }

    private static string? RecognizerFingerprint(IFileRecognizer? recognizer)
    {
        if (recognizer is null)
        {
            return null;
        }
        FileRecognizerDescriptor descriptor = recognizer.Descriptor;
        return string.Join(
            ":",
            recognizer.GetType().AssemblyQualifiedName,
            descriptor.Strategy,
            descriptor.Version,
            descriptor.MaxProbeBytes,
            string.Join(',', descriptor.EvidenceTypes),
            descriptor.CooperativeCancellation);
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
        string ContractVersion,
        Type PortType,
        string DefaultOwnerExtensions,
        string AcceptedInputExtensions,
        string? FileRecognizer,
        string OutputTypes,
        string Capabilities,
        string Formats,
        string Diagnostics,
        string? Json,
        string? Preview,
        string? CommandFactory,
        string? BindingFactory);

    private sealed class ContractCommandHostFactory(ProductCatalog catalog)
        : IProductCommandHostFactory
    {
        public IProductCommandHost<TPort> Create<TPort>(string productId)
            where TPort : class =>
            new ContractCommandHost<TPort>(catalog);
    }

    private sealed class ContractCommandHost<TPort>(ProductCatalog catalog)
        : IProductCommandHost<TPort>
        where TPort : class
    {
        public bool HasCapability<TCapability>(
            ProductCapability<TCapability> slot)
            where TCapability : class =>
            catalog.HasProvider(slot);

        public int Run(
            ParseResult parseResult,
            Func<ProductCommandContext<TPort>, ResultEnvelope> handler) =>
            throw ExecutionUnavailable();
    }

    private static InvalidOperationException ExecutionUnavailable() =>
        new("Product contract command hosts cannot execute commands.");
}
