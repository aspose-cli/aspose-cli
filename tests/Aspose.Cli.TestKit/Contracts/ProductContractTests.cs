using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.CommandLine;
using System.CommandLine.Completions;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Extensibility;
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

    /// <summary>
    /// Field names a listed result object shares with an operation field while meaning
    /// something else, each with the reason; see <see cref="ListedObjects_ShareTheOperationVocabulary"/>.
    /// </summary>
    protected virtual IReadOnlyDictionary<string, string> Homonyms { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

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
        var host = new ContractCommandHostFactory();

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

    /// <summary>Ensures schema, presenter, and Skill resources travel with the product.</summary>
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
        Assert.Contains("Presenter/presenter.js", resources.ResourceNames);
        Assert.Contains("Presenter/presenter.css", resources.ResourceNames);
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
            ["Code", "Details", "Hint", "Message"],
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


    /// <summary>Every product rejects malformed operation discriminators through its own metadata.</summary>
    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"op\":null}")]
    [InlineData("{\"op\":1}")]
    [InlineData("{\"op\":false}")]
    [InlineData("{\"op\":[]}")]
    [InlineData("{\"op\":{}}")]
    [InlineData("{\"op\":\"__unknown_operation__\"}")]
    public void OperationDiscriminators_RejectMalformedInput(string operation)
    {
        JsonSerializerOptions options = new TModule().Define().Json.LocalOptions;
        foreach (ProductSchemaSample sample in OperationInputs())
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(
                "{\"ops\":[" + operation + "]}", sample.Value.GetType(), options));
        }
    }

    /// <summary>Discriminator order is flexible; unknown and duplicate members are never accepted.</summary>
    [Fact]
    public void Operations_RoundTripWithStrictMembersAndDiscriminatorLast()
    {
        JsonSerializerOptions options = new TModule().Define().Json.LocalOptions;
        foreach (ProductSchemaSample sample in OperationInputs())
        {
            Type batchType = sample.Value.GetType();
            JsonObject batch = JsonSerializer.SerializeToNode(sample.Value, batchType, options)!.AsObject();
            foreach (JsonNode? node in batch["ops"]!.AsArray())
            {
                JsonObject operation = node!.AsObject();
                JsonNode discriminator = operation["op"]!.DeepClone();
                operation.Remove("op");
                operation.Add("op", discriminator);
            }

            string json = batch.ToJsonString();
            object restored = JsonSerializer.Deserialize(json, batchType, options)!;
            Assert.True(JsonNode.DeepEquals(batch, JsonSerializer.SerializeToNode(restored, batchType, options)));

            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(
                "{\"ops\":[]," + json[1..], batchType, options));

            string first = batch["ops"]![0]!.ToJsonString();
            foreach (string member in new[] { "\"__unexpected\":true", "\"opName\":\"ignored\"", "\"op\":\"__unknown_operation__\"" })
            {
                Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(
                    "{\"ops\":[{" + member + "," + first[1..] + "]}", batchType, options));
            }
        }
    }

    private IEnumerable<ProductSchemaSample> OperationInputs() => CanonicalInputs.Where(static sample =>
        sample.Value.GetType().BaseType is { IsGenericType: true } type
        && type.GetGenericTypeDefinition() == typeof(BoundedOperationEnvelope<>));

    /// <summary>Checks nested unknown fields using a valid product-owned operation.</summary>
    protected static void AssertOperationObjectIsStrict<TOperation>(string input, string member)
    {
        JsonSerializerOptions options = new TModule().Define().Json.LocalOptions;
        Assert.NotNull(JsonSerializer.Deserialize<TOperation>(input, options));
        JsonObject operation = JsonNode.Parse(input)!.AsObject();
        operation[member]!["__unexpected"] = true;
        JsonException error = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<TOperation>(operation.ToJsonString(), options));
        Assert.Contains("__unexpected", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Checks omitted and explicit operation fields through the production JSON metadata.</summary>
    protected static void AssertOperationDefaults<TOperation>(string input, string expected)
    {
        JsonSerializerOptions options = new TModule().Define().Json!.LocalOptions;
        TOperation operation = JsonSerializer.Deserialize<TOperation>(input, options)!;
        JsonObject actual = JsonSerializer.SerializeToNode(operation, options)!.AsObject();
        foreach ((string name, JsonNode? value) in JsonNode.Parse(expected)!.AsObject())
        {
            Assert.True(JsonNode.DeepEquals(value, actual[name]),
                $"Field '{name}' in {input}: expected {value}, actual {actual[name]}.");
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
    /// Ensures every manifest operation has a self-contained schema view that parses and is
    /// narrowed to that operation.
    /// </summary>
    [Fact]
    public void Operations_HaveNarrowedSchemaViews()
    {
        ProductCatalog catalog = ProductCatalog.Build([new TModule()]);
        foreach (ProductOperationDescriptor operation in Assert.Single(catalog.Products).Manifest.Operations
            .Select(static command => command.Descriptor))
        {
            foreach (string name in operation.Ops)
            {
                Assert.True(catalog.Resources.TryReadOperation(operation.InputSchema, name, out string? view));
                _ = ParseSchema(view);
                JsonNode oneOf = JsonNode.Parse(view)!["properties"]!["ops"]!["items"]!["oneOf"]!;
                Assert.Equal($"#/$defs/{name}", Assert.Single(oneOf.AsArray())!["$ref"]!.GetValue<string>());
            }
        }
    }

    /// <summary>
    /// Keeps the committed copy of every generated operation schema equal to the schema the
    /// build serves. Set <c>ASPOSE_CLI_TEST_UPDATE_SNAPSHOTS=1</c> to rewrite the copy, then review the diff.
    /// </summary>
    [Fact]
    public void GeneratedOperationSchemas_MatchTheirCommittedCopies()
    {
        ProductCatalog catalog = ProductCatalog.Build([new TModule()]);
        ProductDefinition definition = Assert.Single(catalog.Products);
        foreach (string schemaId in definition.Manifest.Operations
            .Select(static operation => operation.Descriptor.InputSchema)
            .Distinct(StringComparer.Ordinal))
        {
            string path = Path.Combine(
                RepositoryPaths.Root, "src", typeof(TModule).Assembly.GetName().Name!, "Schemas", "v2",
                schemaId[(schemaId.LastIndexOf('/') + 1)..] + ".schema.json");
            string served = catalog.Resources.Read(schemaId);
            if (Environment.GetEnvironmentVariable(UpdateSnapshotsVariable) == "1")
            {
                File.WriteAllText(path, served, new System.Text.UTF8Encoding(false));
                continue;
            }

            Assert.True(File.Exists(path), $"{path} is missing; set {UpdateSnapshotsVariable}=1 to write it.");
            Assert.True(
                string.Equals(File.ReadAllText(path).ReplaceLineEndings("\n"), served, StringComparison.Ordinal),
                $"{path} differs from the schema generated from the operation records; set {UpdateSnapshotsVariable}=1, rerun and review the diff.");
        }
    }

    /// <summary>
    /// Keeps the objects results list in the edit vocabulary. A field of a listed object
    /// (an item of a result array, such as a sheet, chart, shape, block, page or bookmark,
    /// and anything nested in it) that shares its name with an operation field has a JSON type
    /// the operations accept and, where both state allowed values, only values they accept, so
    /// what is read can be written back under the same name. Result metadata outside lists is
    /// not a document object and is not compared; a genuine homonym is named in
    /// <see cref="Homonyms"/> with its reason.
    /// </summary>
    [Fact]
    public void ListedObjects_ShareTheOperationVocabulary()
    {
        ProductCatalog catalog = ProductCatalog.Build([new TModule()]);
        ProductDefinition definition = Assert.Single(catalog.Products);
        string[] operationSchemas = definition.Manifest.Operations
            .Select(static operation => operation.Descriptor.InputSchema)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (operationSchemas.Length == 0)
        {
            return;
        }

        var edit = new Dictionary<string, List<FieldShape>>(StringComparer.Ordinal);
        foreach (string id in operationSchemas)
        {
            JsonObject document = JsonNode.Parse(catalog.Resources.Read(id))!.AsObject();
            CollectFields(document, document, listed: true, edit, []);
        }

        var mismatches = new List<string>();
        foreach (string id in catalog.Resources.GetProduct(definition.Manifest.Id).SchemaIds
            .Except(operationSchemas, StringComparer.Ordinal))
        {
            var read = new Dictionary<string, List<FieldShape>>(StringComparer.Ordinal);
            JsonObject document = JsonNode.Parse(catalog.Resources.Read(id))!.AsObject();
            CollectFields(document, document, listed: false, read, []);
            foreach ((string name, List<FieldShape> shapes) in read)
            {
                if (name is "op" or "id" || Homonyms.ContainsKey(name)
                    || !edit.TryGetValue(name, out List<FieldShape>? accepted))
                {
                    continue;
                }

                foreach (FieldShape shape in shapes.Where(shape => !accepted.Any(shape.FitsIn)).Distinct())
                {
                    mismatches.Add(
                        $"{id}: '{name}' is {shape}, but operations accept {string.Join(" or ", accepted.Distinct())}");
                }
            }
        }

        Assert.True(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches));
    }

    /// <summary>
    /// Keeps every operation document a product Skill shows valid against the generated schema:
    /// each example <c>*.json</c> file and each fenced <c>json</c> block in the Skill's Markdown
    /// that holds an <c>ops</c> array. A Skill therefore cannot teach a field, value or shape the
    /// build rejects.
    /// </summary>
    [Fact]
    public void SkillOperationDocuments_ConformToTheGeneratedSchema()
    {
        ProductCatalog catalog = ProductCatalog.Build([new TModule()]);
        ProductDefinition definition = Assert.Single(catalog.Products);
        string[] operationSchemas = definition.Manifest.Operations
            .Select(static operation => operation.Descriptor.InputSchema)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string skills = Path.Combine(RepositoryPaths.Root, "src", typeof(TModule).Assembly.GetName().Name!, "Skills");
        if (operationSchemas.Length == 0 || !Directory.Exists(skills))
        {
            return;
        }

        JsonSchema[] schemas = [.. operationSchemas.Select(id => ParseSchema(catalog.Resources.Read(id)))];
        var documents = new List<(string Source, string Json)>();
        foreach (string file in Directory.EnumerateFiles(skills, "*", SearchOption.AllDirectories))
        {
            if (file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                documents.Add((file, File.ReadAllText(file)));
            }
            else if (file.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                documents.AddRange(FencedJson(File.ReadAllText(file)).Select(block => (file, block)));
            }
        }

        var failures = new List<string>();
        foreach ((string source, string json) in documents)
        {
            JsonNode? node;
            try
            {
                node = JsonNode.Parse(json);
            }
            catch (System.Text.Json.JsonException)
            {
                continue;
            }

            if (node is not JsonObject { } document || document["ops"] is not JsonArray)
            {
                continue;
            }

            using JsonDocument instance = JsonDocument.Parse(json);
            if (!schemas.Any(schema => schema.Evaluate(instance.RootElement).IsValid))
            {
                failures.Add($"{Path.GetRelativePath(RepositoryPaths.Root, source)}: {json.ReplaceLineEndings(" ")[..Math.Min(json.Length, 160)]}");
            }
        }

        Assert.True(failures.Count == 0, "Skill operation documents the schema rejects:" + Environment.NewLine
            + string.Join(Environment.NewLine, failures));
    }

    private static IEnumerable<string> FencedJson(string markdown)
    {
        string[] lines = markdown.ReplaceLineEndings("\n").Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            if (!lines[index].TrimStart().StartsWith("```json", StringComparison.Ordinal))
            {
                continue;
            }

            var block = new System.Text.StringBuilder();
            for (index++; index < lines.Length && !lines[index].TrimStart().StartsWith("```", StringComparison.Ordinal); index++)
            {
                block.AppendLine(lines[index]);
            }

            yield return block.ToString();
        }
    }

    /// <summary>The JSON types and allowed values one schema states for a named field.</summary>
    private sealed record FieldShape(string Types, string? Values)
    {
        public bool FitsIn(FieldShape accepted)
        {
            string[] offered = accepted.Types.Split('|');
            bool typeFits = Types.Length == 0 || accepted.Types.Length == 0
                || Types.Split('|').All(type =>
                    offered.Contains(type) || (type == "integer" && offered.Contains("number")));
            return typeFits
                && (Values is null || accepted.Values is null
                    || Values.Split('|').All(accepted.Values.Split('|').Contains));
        }

        public override string ToString() =>
            (Types.Length == 0 ? "untyped" : Types) + (Values is null ? string.Empty : $" [{Values}]");

        public static FieldShape Of(JsonObject schema)
        {
            IEnumerable<string> types = schema["type"] switch
            {
                JsonValue single => [single.GetValue<string>()],
                JsonArray several => several.Select(static type => type!.GetValue<string>()),
                _ => [],
            };
            JsonNode?[]? values = schema["enum"] is JsonArray allowed ? [.. allowed]
                : schema["const"] is JsonNode fixedValue ? [fixedValue] : null;
            return new FieldShape(
                string.Join('|', types.Where(static type => type != "null").Order(StringComparer.Ordinal)),
                values is null ? null : string.Join('|', values
                    .Where(static value => value is not null)
                    .Select(static value => value!.ToJsonString())
                    .Order(StringComparer.Ordinal)));
        }
    }

    /// <summary>
    /// Collects the named properties of <paramref name="node"/> and everything below it,
    /// following local references once each; a property counts only once the walk is inside a
    /// listed object (below an array's items) unless <paramref name="listed"/> starts true.
    /// </summary>
    private static void CollectFields(
        JsonObject document,
        JsonNode node,
        bool listed,
        Dictionary<string, List<FieldShape>> fields,
        HashSet<string> followed)
    {
        if (node is JsonArray array)
        {
            foreach (JsonNode? child in array)
            {
                if (child is not null)
                {
                    CollectFields(document, child, listed, fields, followed);
                }
            }

            return;
        }

        if (node is not JsonObject schema)
        {
            return;
        }

        if (schema["$ref"] is JsonValue reference
            && reference.GetValue<string>() is { } target
            && target.StartsWith("#/$defs/", StringComparison.Ordinal)
            && followed.Add($"{listed}:{target}"))
        {
            CollectFields(document, document["$defs"]![target["#/$defs/".Length..]]!, listed, fields, followed);
        }

        foreach ((string key, JsonNode? child) in schema)
        {
            if (child is null || key is "$defs" or "$ref")
            {
                continue;
            }

            if (key == "properties" && child is JsonObject properties)
            {
                foreach ((string name, JsonNode? value) in properties)
                {
                    if (value is not JsonObject property)
                    {
                        continue;
                    }

                    if (listed)
                    {
                        if (!fields.TryGetValue(name, out List<FieldShape>? shapes))
                        {
                            fields[name] = shapes = [];
                        }

                        shapes.Add(FieldShape.Of(Resolve(document, property)));
                    }

                    CollectFields(document, property, listed, fields, followed);
                }

                continue;
            }

            CollectFields(document, child, listed || key is "items" or "prefixItems", fields, followed);
        }
    }

    private static JsonObject Resolve(JsonObject document, JsonObject property) =>
        property["$ref"] is JsonValue reference
        && reference.GetValue<string>() is { } target
        && target.StartsWith("#/$defs/", StringComparison.Ordinal)
            ? document["$defs"]![target["#/$defs/".Length..]]!.AsObject()
            : property;

    private const string UpdateSnapshotsVariable = Aspose.Cli.Sdk.DistributionInfo.EnvironmentVariablePrefix + "TEST_UPDATE_SNAPSHOTS";

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
                $"{definition.View.ProductId}:{definition.View.PortType.AssemblyQualifiedName}",
                $"{definition.View.ReviewView}:{definition.View.LiveView}:{definition.View.VisualInspectionRequired}",
                string.Join(
                    ",",
                    definition.View.Views.Select(
                        static view => $"{view.Id}:{view.Label}:{view.PartKind}"))),
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
        string Formats,
        string Diagnostics,
        string? Json,
        string? View,
        string? CommandFactory,
        string? BindingFactory);

    private sealed class ContractCommandHostFactory
        : IProductCommandHostFactory
    {
        public IProductCommandHost<TPort> Create<TPort>(string productId)
            where TPort : class =>
            new ContractCommandHost<TPort>();
    }

    private sealed class ContractCommandHost<TPort>
        : IProductCommandHost<TPort>
        where TPort : class
    {
        public int Run(
            ParseResult parseResult,
            Func<ProductCommandContext<TPort>, ResultEnvelope> handler) =>
            throw ExecutionUnavailable();
    }

    private static InvalidOperationException ExecutionUnavailable() =>
        new("Product contract command hosts cannot execute commands.");
}
