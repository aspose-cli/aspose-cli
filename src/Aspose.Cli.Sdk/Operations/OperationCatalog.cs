using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Sdk.Operations;

/// <summary>
/// A product's operation vocabulary: the only place an operation is registered. Each entry
/// binds a wire name to its record type and its rules. The catalog feeds the discriminator
/// converter, capabilities, the schema and the parse pipeline, so the name, type, validation
/// and published order cannot drift apart. A vocabulary declared with
/// <see cref="OperationVocabularyAttribute"/> gets a generated catalog built from its records'
/// descriptors, which also owns the vocabulary's defaults, schema, input paths and secrets.
/// </summary>
public sealed class OperationCatalog<TOp>
    where TOp : BoundedOperation
{
    private readonly List<string> _names = [];
    private readonly Dictionary<string, Type> _types = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, Entry> _entries = [];
    private readonly GeneratedOperationSchema? _schema;

    /// <summary>Creates an empty vocabulary for one operation document contract.</summary>
    /// <param name="schemaId">Canonical schema identifier of the operation document.</param>
    /// <param name="maximumOperations">Largest accepted number of operations in one document.</param>
    public OperationCatalog(string schemaId, int maximumOperations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaId);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumOperations, 1);
        SchemaId = schemaId;
        MaximumOperations = maximumOperations;
        string relative = schemaId.StartsWith(DistributionInfo.SchemaBaseUri, StringComparison.Ordinal)
            ? schemaId[DistributionInfo.SchemaBaseUri.Length..]
            : schemaId;
        SchemaCommandId = "v2/" + relative.Replace(".schema.json", string.Empty, StringComparison.Ordinal);
        DefaultHint = $"Fix the named operation; '{DistributionInfo.CommandName} schema {SchemaCommandId}' documents every operation.";
    }

    /// <summary>
    /// Creates a generated vocabulary from its operation descriptors, published in ordinal
    /// order of their wire names. Only generated code calls this constructor.
    /// </summary>
    /// <param name="schemaId">Canonical schema identifier of the operation document.</param>
    /// <param name="maximumOperations">Largest accepted number of operations in one document.</param>
    /// <param name="description">The vocabulary's description, published on the schema.</param>
    /// <param name="contracts">Source-generated JSON metadata for every operation record.</param>
    /// <param name="operations">The operation descriptors.</param>
    public OperationCatalog(
        string schemaId,
        int maximumOperations,
        string? description,
        IJsonTypeInfoResolver contracts,
        IReadOnlyList<OperationDescriptor> operations)
        : this(schemaId, maximumOperations)
    {
        ArgumentNullException.ThrowIfNull(contracts);
        ArgumentNullException.ThrowIfNull(operations);
        Contracts = contracts;
        foreach (OperationDescriptor operation in operations.OrderBy(static operation => operation.Record.Name, StringComparer.Ordinal))
        {
            if (!operation.Record.Type.IsAssignableTo(typeof(TOp)))
            {
                throw new ArgumentException($"Operation type {operation.Record.Type.Name} is not a {typeof(TOp).Name}.", nameof(operations));
            }

            Register(operation.Record.Name, operation.Record.Type, new Entry(operation.Record.Name, null, operation));
        }

        OperationRecord[] records = [.. operations.Select(static operation => operation.Record).OrderBy(static record => record.Name, StringComparer.Ordinal)];
        _schema = new GeneratedOperationSchema(() => OperationSchemaWriter.Write(SchemaId, MaximumOperations, description, records), _names);
    }

    /// <summary>Canonical schema identifier of the operation document.</summary>
    public string SchemaId { get; }

    /// <summary>Largest accepted number of operations in one document.</summary>
    public int MaximumOperations { get; }

    /// <summary>Wire names in published order.</summary>
    public IReadOnlyList<string> Names => _names;

    /// <summary>Wire name to record type, for the discriminator converter.</summary>
    public IReadOnlyDictionary<string, Type> Registry => _types;

    internal string DefaultHint { get; }

    /// <summary>The generated vocabulary's operation metadata; null for a registered one.</summary>
    internal IJsonTypeInfoResolver? Contracts { get; }

    private string SchemaCommandId { get; }

    /// <summary>Registers one operation. Append new operations: the order is published.</summary>
    /// <param name="name">Stable wire name.</param>
    /// <param name="validate">
    /// Pure semantic validation that returns the normalized operation, or rejects it with
    /// <see cref="OperationInvalidException"/>. Omitted when the contract types say everything.
    /// </param>
    public OperationCatalog<TOp> Add<T>(string name, Func<T, T>? validate = null)
        where T : TOp
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (_schema is not null)
        {
            throw new InvalidOperationException("A generated vocabulary takes no registered operations.");
        }

        Register(name, typeof(T), new Entry(name, validate is null ? null : op => validate((T)op), null));
        return this;
    }

    /// <summary>Registers one operation whose validation only checks and never normalizes.</summary>
    public OperationCatalog<TOp> Add<T>(string name, Action<T> check)
        where T : TOp
    {
        ArgumentNullException.ThrowIfNull(check);
        return Add<T>(name, op =>
        {
            check(op);
            return op;
        });
    }

    /// <summary>
    /// Declares the command that applies documents of this vocabulary, for the product manifest;
    /// a generated vocabulary's schema travels with it to the product resources.
    /// </summary>
    public ProductOperationCommand Describe(string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        return new ProductOperationCommand(
            new ProductOperationDescriptor
            {
                Command = command,
                InputSchema = SchemaCommandId,
                OperationSchema = $"{DistributionInfo.CommandName} schema {SchemaCommandId} --operation <op>",
                MaximumOperations = MaximumOperations,
                Ops = Names,
            },
            _schema);
    }

    /// <summary>Returns the wire name of a registered operation.</summary>
    public string NameOf(TOp operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return EntryOf(operation).Name;
    }

    /// <summary>Parses, validates and identifies an operation document.</summary>
    /// <exception cref="CliException"><c>OPS_INVALID</c>, with the index of a failing operation.</exception>
    public TBatch Parse<TBatch>(string json, ProductJsonDefinition contracts)
        where TBatch : BoundedOperationEnvelope<TOp>
    {
        ArgumentNullException.ThrowIfNull(contracts);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw Invalid("the document is empty");
        }

        using JsonDocument document = ParseJson(json);
        TBatch batch;
        try
        {
            batch = contracts.Deserialize<TBatch>(json);
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or NotSupportedException)
        {
            throw Rejected(document.RootElement, typeof(TBatch), contracts.LocalOptions, exception as JsonException);
        }

        return Prepare(batch);
    }

    /// <summary>
    /// Validates a parsed or composed document once and assigns deterministic ids to
    /// operations without one. Each operation is checked against its declared constraints,
    /// then its own <see cref="BoundedOperation.Validated"/> rules, which may normalize it.
    /// </summary>
    /// <exception cref="CliException"><c>OPS_INVALID</c>, with the index of a failing operation.</exception>
    public TBatch Prepare<TBatch>(TBatch batch)
        where TBatch : BoundedOperationEnvelope<TOp>
    {
        ArgumentNullException.ThrowIfNull(batch);
        BoundedOperationValidation.ValidateEnvelope(batch, SchemaId, Invalid);
        if (batch.Ops.Count < 1 || batch.Ops.Count > MaximumOperations)
        {
            throw Invalid($"the ops array must contain 1-{MaximumOperations} operations");
        }

        IReadOnlyList<TOp> identified = BoundedOperationIds.Assign(
            batch.Ops,
            static operation => operation.Id,
            static (operation, id) => (TOp)((BoundedOperation)operation with { Id = id }),
            Invalid);
        var validated = new TOp[identified.Count];
        for (int index = 0; index < identified.Count; index++)
        {
            TOp operation = identified[index];
            Entry entry = EntryOf(operation);
            try
            {
                if (entry.Operation is { } descriptor)
                {
                    OperationContractValidator.Check(descriptor.Record, operation);
                }

                TOp normalized = operation.Validated() as TOp
                    ?? throw new InvalidOperationException($"{operation.GetType().Name}.Validated() must return a {typeof(TOp).Name}.");
                validated[index] = entry.Validate?.Invoke(normalized) ?? normalized;
            }
            catch (OperationInvalidException rejection)
            {
                throw OperationErrors.InvalidAt(index, entry.Name, rejection.Message, rejection.Hint ?? DefaultHint);
            }
            catch (CliException failure) when (!failure.IsInvocationFailure)
            {
                // A shared value parser (a page range, an address) rejected a field.
                throw OperationErrors.InvalidAt(index, entry.Name, failure.Message, failure.Hint ?? DefaultHint, failure.Code);
            }
        }

        return (TBatch)((BoundedOperationEnvelope<TOp>)batch with { SchemaVersion = 2, Ops = validated });
    }

    /// <summary>Finds a named operation's record type and, for a generated vocabulary, its record.</summary>
    internal bool TryGetOperation(string name, out Type type, out OperationRecord? record)
    {
        record = null;
        if (!_types.TryGetValue(name, out type!))
        {
            return false;
        }

        record = _entries[type].Operation?.Record;
        return true;
    }

    /// <summary>Resolves the files a generated operation reads; any other operation is returned unchanged.</summary>
    internal TOp ResolveInputPaths(TOp operation, Func<string, string> resolve) =>
        EntryOf(operation).Operation?.ResolveInputPaths is { } resolveInputs
            ? (TOp)resolveInputs(operation, resolve)
            : operation;

    /// <summary>The environment variables whose secrets a generated operation reads; null entries are omitted fields.</summary>
    internal IEnumerable<string?> SecretVariables(TOp operation) =>
        EntryOf(operation).Operation?.SecretVariables?.Invoke(operation) ?? [];

    private void Register(string name, Type type, Entry entry)
    {
        if (_types.ContainsKey(name))
        {
            throw new ArgumentException($"Operation '{name}' is registered twice.", nameof(name));
        }
        if (_entries.ContainsKey(type))
        {
            throw new ArgumentException($"Operation type {type.Name} is registered twice.", nameof(name));
        }
        _types.Add(name, type);
        _entries.Add(type, entry);
        _names.Add(name);
    }

    private JsonDocument ParseJson(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw Invalid($"the document is not valid JSON: {exception.Message}");
        }
    }

    /// <summary>
    /// Restates a serializer rejection for the agent. The operation converter already words its
    /// failures in wire terms and the serializer records the failing operation's position, so
    /// such a failure keeps its text and gains the index and name; any other failure is
    /// explained from the document against the envelope contract.
    /// </summary>
    private CliException Rejected(JsonElement root, Type batchType, JsonSerializerOptions options, JsonException? rejection)
    {
        if (OperationIndex(rejection?.Path) is { } index)
        {
            return OperationErrors.InvalidAt(index, RegisteredNameAt(root, index), rejection!.Message, DefaultHint);
        }

        return Invalid(JsonContractDiagnostics.Explain(root, batchType, options, rejection?.Path));
    }

    private static int? OperationIndex(string? path)
    {
        const string Prefix = "$.ops[";
        return path is not null && path.StartsWith(Prefix, StringComparison.Ordinal) && path.EndsWith(']')
            && int.TryParse(path.AsSpan(Prefix.Length, path.Length - Prefix.Length - 1),
                NumberStyles.None, CultureInfo.InvariantCulture, out int index)
                ? index
                : null;
    }

    private string? RegisteredNameAt(JsonElement root, int index) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty("ops", out JsonElement ops) && ops.ValueKind == JsonValueKind.Array
        && index < ops.GetArrayLength()
        && ops[index] is { ValueKind: JsonValueKind.Object } operation
        && operation.TryGetProperty("op", out JsonElement name) && name.ValueKind == JsonValueKind.String
        && _types.ContainsKey(name.GetString()!)
            ? name.GetString()
            : null;

    private Entry EntryOf(TOp operation) =>
        _entries.TryGetValue(operation.GetType(), out Entry? entry)
            ? entry
            : throw new InvalidOperationException(
                $"Operation type {operation.GetType().Name} is not registered in its catalog.");

    private CliException Invalid(string reason) => OperationErrors.Invalid(reason, DefaultHint);

    private sealed record Entry(string Name, Func<TOp, TOp>? Validate, OperationDescriptor? Operation);
}
