using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Aspose.Cli.Sdk.Serialization;

/// <summary>Source-generated JSON metadata contributed by one product.</summary>
public sealed class ProductJsonDefinition
{
    private readonly JsonSerializerOptions _localOptions;

    /// <summary>Creates a product JSON contribution.</summary>
    public ProductJsonDefinition(
        string productId,
        IJsonTypeInfoResolver resolver,
        IEnumerable<JsonConverter>? converters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        ArgumentNullException.ThrowIfNull(resolver);
        ProductId = productId;
        Resolver = resolver;
        Converters = Array.AsReadOnly(
            converters?.ToArray() ?? []);
        ResultRecords = SdkSchemaCatalog.ResultRecordsOf(resolver);
        _localOptions = ContractJsonSerializer.CreateOptions(
            [SdkJsonContext.Default, resolver],
            Converters);
    }

    /// <summary>Stable identifier of the contributing product.</summary>
    public string ProductId { get; }

    /// <summary>Source-generated resolver owned by the product assembly.</summary>
    public IJsonTypeInfoResolver Resolver { get; }

    /// <summary>Product-local converters required by abstract contract roots.</summary>
    public IReadOnlyList<JsonConverter> Converters { get; }

    /// <summary>
    /// The result records the product's contract generator described, which the catalog
    /// publishes under the product's schema ids.
    /// </summary>
    public IReadOnlyList<Contracts.ResultRecord> ResultRecords { get; }

    /// <summary>Frozen options for product-local contract tests and parsers.</summary>
    public JsonSerializerOptions LocalOptions => _localOptions;

    /// <summary>Deserializes one product-local contract root.</summary>
    public T Deserialize<T>(string json)
    {
        ArgumentException.ThrowIfNullOrEmpty(json);
        return JsonSerializer.Deserialize<T>(json, _localOptions)
            ?? throw new JsonException(
                $"JSON deserialized to null for {typeof(T).Name}.");
    }
}

/// <summary>
/// Immutable serializer composed once from SDK and registered product metadata.
/// </summary>
public sealed class ContractJsonSerializer
{
    private readonly JsonSerializerOptions _options;
    private readonly JsonSerializerOptions _compactOptions;

    /// <summary>Creates a serializer for the supplied product contributions.</summary>
    public ContractJsonSerializer(
        IEnumerable<ProductJsonDefinition> products)
    {
        ArgumentNullException.ThrowIfNull(products);
        ProductJsonDefinition[] definitions = products.ToArray();
        var resolvers = new List<IJsonTypeInfoResolver> { SdkJsonContext.Default };
        resolvers.AddRange(definitions.Select(static definition => definition.Resolver));

        JsonConverter[] converters = definitions
            .SelectMany(static definition => definition.Converters)
            .ToArray();
        _options = CreateOptions(resolvers, converters);
        _compactOptions = new JsonSerializerOptions(_options) { WriteIndented = false };
        _compactOptions.MakeReadOnly();
    }

    /// <summary>Serializes a contract value using its runtime type.</summary>
    public string Serialize(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return JsonSerializer.Serialize(value, value.GetType(), _options);
    }

    /// <summary>Serializes a contract value on one line, with the same content as <see cref="Serialize"/>.</summary>
    public string SerializeCompact(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return JsonSerializer.Serialize(value, value.GetType(), _compactOptions);
    }

    internal static JsonSerializerOptions CreateOptions(
        IEnumerable<IJsonTypeInfoResolver> resolvers,
        IEnumerable<JsonConverter> converters)
    {
        IJsonTypeInfoResolver[] resolverArray = resolvers.ToArray();
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = JsonTypeInfoResolver.Combine(resolverArray),
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            AllowOutOfOrderMetadataProperties = true,
            AllowDuplicateProperties = false,
            // Contract input is written by an agent: an unknown member is a mistake to report,
            // never a field to ignore, whatever record it appears in.
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            // 'required' only demands presence; an explicit null in a non-nullable contract
            // member is also a wire error, not a value for product code to trip over.
            RespectNullableAnnotations = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true,
        };
        foreach (JsonConverter converter in converters)
        {
            options.Converters.Add(converter);
        }
        options.MakeReadOnly();
        return options;
    }
}
