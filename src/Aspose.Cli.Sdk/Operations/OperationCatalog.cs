using System.Text.Json;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Sdk.Operations;

/// <summary>
/// A product's operation vocabulary: the only place an operation is registered. Each entry
/// binds a wire name to its record type and a pure validator, in documentation order. The
/// catalog feeds the discriminator converter, capabilities and the parse pipeline, so the
/// name, type, validation and published order cannot drift apart.
/// </summary>
public sealed class OperationCatalog<TOp>
    where TOp : BoundedOperation
{
    private readonly List<string> _names = [];
    private readonly Dictionary<string, Type> _types = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, Entry> _entries = [];

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

    /// <summary>Canonical schema identifier of the operation document.</summary>
    public string SchemaId { get; }

    /// <summary>Largest accepted number of operations in one document.</summary>
    public int MaximumOperations { get; }

    /// <summary>Wire names in registration (documentation) order.</summary>
    public IReadOnlyList<string> Names => _names;

    /// <summary>Wire name to record type, for the discriminator converter.</summary>
    public IReadOnlyDictionary<string, Type> Registry => _types;

    internal string DefaultHint { get; }

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
        if (_types.ContainsKey(name))
        {
            throw new ArgumentException($"Operation '{name}' is registered twice.", nameof(name));
        }
        if (_entries.ContainsKey(typeof(T)))
        {
            throw new ArgumentException($"Operation type {typeof(T).Name} is registered twice.", nameof(name));
        }
        _types.Add(name, typeof(T));
        _entries.Add(typeof(T), new Entry(name, validate is null ? null : op => validate((T)op)));
        _names.Add(name);
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

    /// <summary>Describes the command that applies documents of this vocabulary.</summary>
    public ProductOperationDescriptor Describe(string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        return new ProductOperationDescriptor
        {
            Command = command,
            InputSchema = SchemaCommandId,
            MaximumOperations = MaximumOperations,
            Ops = Names,
        };
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

        TBatch batch;
        try
        {
            batch = contracts.Deserialize<TBatch>(json);
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or NotSupportedException)
        {
            throw Invalid(exception.Message);
        }

        return Prepare(batch);
    }

    /// <summary>
    /// Validates a parsed or composed document once and assigns deterministic ids to
    /// operations without one.
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
                validated[index] = entry.Validate?.Invoke(operation) ?? operation;
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

    private Entry EntryOf(TOp operation) =>
        _entries.TryGetValue(operation.GetType(), out Entry? entry)
            ? entry
            : throw new InvalidOperationException(
                $"Operation type {operation.GetType().Name} is not registered in its catalog.");

    private CliException Invalid(string reason) => OperationErrors.Invalid(reason, DefaultHint);

    private sealed record Entry(string Name, Func<TOp, TOp>? Validate);
}
