using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Sdk.Operations;

/// <summary>
/// A product's operation vocabulary, generated from the records of its
/// <see cref="OperationVocabularyAttribute"/> base. It binds each wire name to its record and
/// feeds the discriminator converter, capabilities, the schema and the parse pipeline, so the
/// name, type, defaults, validation, input paths and secrets cannot drift apart.
/// </summary>
public sealed class OperationCatalog<TOp>
    where TOp : BoundedOperation
{
    private readonly Dictionary<string, OperationDescriptor> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, OperationDescriptor> _byType = [];
    private readonly GeneratedOperationSchema _schema;

    /// <summary>
    /// Creates a vocabulary from its operation descriptors, published in ordinal order of their
    /// wire names. Only generated code calls this constructor.
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
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaId);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumOperations, 1);
        ArgumentNullException.ThrowIfNull(contracts);
        ArgumentNullException.ThrowIfNull(operations);
        SchemaId = schemaId;
        MaximumOperations = maximumOperations;
        Contracts = contracts;
        string relative = schemaId.StartsWith(DistributionInfo.SchemaBaseUri, StringComparison.Ordinal)
            ? schemaId[DistributionInfo.SchemaBaseUri.Length..]
            : schemaId;
        SchemaCommandId = "v2/" + relative.Replace(".schema.json", string.Empty, StringComparison.Ordinal);
        DefaultHint = $"Fix the named operation; '{DistributionInfo.CommandName} schema {SchemaCommandId}' documents every operation.";
        OperationDescriptor[] ordered = [.. operations.OrderBy(static operation => operation.Record.Name, StringComparer.Ordinal)];
        foreach (OperationDescriptor operation in ordered)
        {
            Type type = operation.Record.Type;
            if (!type.IsAssignableTo(typeof(TOp)))
            {
                throw new ArgumentException($"Operation type {type.Name} is not a {typeof(TOp).Name}.", nameof(operations));
            }
            if (!_byName.TryAdd(operation.Record.Name, operation) || !_byType.TryAdd(type, operation))
            {
                throw new ArgumentException($"Operation '{operation.Record.Name}' ({type.Name}) is declared twice.", nameof(operations));
            }
        }

        Names = [.. ordered.Select(static operation => operation.Record.Name)];
        OperationRecord[] records = [.. ordered.Select(static operation => operation.Record)];
        _schema = new GeneratedOperationSchema(() => OperationSchemaWriter.Write(SchemaId, MaximumOperations, description, records), Names);
    }

    /// <summary>Canonical schema identifier of the operation document.</summary>
    public string SchemaId { get; }

    /// <summary>Largest accepted number of operations in one document.</summary>
    public int MaximumOperations { get; }

    /// <summary>Wire names in published order.</summary>
    public IReadOnlyList<string> Names { get; }

    internal string DefaultHint { get; }

    /// <summary>Source-generated JSON metadata for every operation record.</summary>
    internal IJsonTypeInfoResolver Contracts { get; }

    /// <summary>The schema's id in the <c>schema</c> command, such as <c>v2/pdf/ops</c>.</summary>
    internal string SchemaCommandId { get; }

    /// <summary>
    /// Declares the command that applies documents of this vocabulary, for the product manifest;
    /// the vocabulary's schema travels with it to the product resources.
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
                Schema = _schema,
            });
    }

    /// <summary>Returns the wire name of an operation of this vocabulary.</summary>
    public string NameOf(TOp operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return DescriptorOf(operation).Record.Name;
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
            OperationRecord record = DescriptorOf(operation).Record;
            try
            {
                OperationContractValidator.Check(record, operation);
                validated[index] = operation.Validated() as TOp
                    ?? throw new InvalidOperationException($"{operation.GetType().Name}.Validated() must return a {typeof(TOp).Name}.");
            }
            catch (OperationInvalidException rejection)
            {
                throw OperationErrors.InvalidAt(index, record.Name, rejection.Message, rejection.Hint ?? DefaultHint);
            }
            catch (CliException failure) when (!failure.IsInvocationFailure)
            {
                // A shared value parser (a page range, an address) rejected a field.
                throw OperationErrors.InvalidAt(index, record.Name, failure.Message, failure.Hint ?? DefaultHint, failure.Code);
            }
        }

        return (TBatch)((BoundedOperationEnvelope<TOp>)batch with { SchemaVersion = 2, Ops = validated });
    }

    /// <summary>Finds a named operation's record.</summary>
    internal bool TryGetOperation(string name, [NotNullWhen(true)] out OperationRecord? record)
    {
        record = _byName.TryGetValue(name, out OperationDescriptor? operation) ? operation.Record : null;
        return record is not null;
    }

    /// <summary>Resolves the files an operation reads; an operation that reads none is returned unchanged.</summary>
    internal TOp ResolveInputPaths(TOp operation, Func<string, string> resolve) =>
        DescriptorOf(operation).ResolveInputPaths is { } resolveInputs
            ? (TOp)resolveInputs(operation, resolve)
            : operation;

    /// <summary>The environment variables whose secrets an operation reads; null entries are omitted fields.</summary>
    internal IEnumerable<string?> SecretVariables(TOp operation) =>
        DescriptorOf(operation).SecretVariables?.Invoke(operation) ?? [];

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
            return OperationErrors.InvalidAt(
                index, KnownNameAt(root, index), rejection!.Message, DefaultHint, field: rejection as UnknownFieldException);
        }

        JsonException reason = JsonContractDiagnostics.Explain(root, batchType, options, rejection?.Path);
        return OperationErrors.Invalid(reason.Message, DefaultHint, reason as UnknownFieldException);
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

    private string? KnownNameAt(JsonElement root, int index) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty("ops", out JsonElement ops) && ops.ValueKind == JsonValueKind.Array
        && index < ops.GetArrayLength()
        && ops[index] is { ValueKind: JsonValueKind.Object } operation
        && operation.TryGetProperty("op", out JsonElement name) && name.ValueKind == JsonValueKind.String
        && _byName.ContainsKey(name.GetString()!)
            ? name.GetString()
            : null;

    private OperationDescriptor DescriptorOf(TOp operation) =>
        _byType.TryGetValue(operation.GetType(), out OperationDescriptor? descriptor)
            ? descriptor
            : throw new InvalidOperationException(
                $"Operation type {operation.GetType().Name} is not an operation of its catalog.");

    private CliException Invalid(string reason) => OperationErrors.Invalid(reason, DefaultHint);
}
