using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Serialization;
using Aspose.Cli.Sdk.Text;

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
    private readonly Dictionary<string, string> _byMistakenName = new(StringComparer.OrdinalIgnoreCase);
    private readonly GeneratedOperationSchema _schema;

    /// <summary>
    /// Creates a vocabulary from its operation descriptors, published in ordinal order of their
    /// wire names. Only generated code calls this constructor.
    /// </summary>
    /// <param name="schemaId">Canonical schema identifier of the operation document.</param>
    /// <param name="maximumOperationCount">Largest accepted number of operations in one document.</param>
    /// <param name="description">The vocabulary's description, published on the schema.</param>
    /// <param name="contracts">Source-generated JSON metadata for every operation record.</param>
    /// <param name="operations">The operation descriptors.</param>
    public OperationCatalog(
        string schemaId,
        int maximumOperationCount,
        string? description,
        IJsonTypeInfoResolver contracts,
        IReadOnlyList<OperationDescriptor> operations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaId);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumOperationCount, 1);
        ArgumentNullException.ThrowIfNull(contracts);
        ArgumentNullException.ThrowIfNull(operations);
        SchemaId = schemaId;
        MaximumOperationCount = maximumOperationCount;
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
        foreach (OperationRecord record in ordered.Select(static operation => operation.Record))
        {
            foreach (string mistaken in record.MistakenFor)
            {
                if (Names.Contains(mistaken, StringComparer.OrdinalIgnoreCase) || !_byMistakenName.TryAdd(mistaken, record.Name))
                {
                    throw new ArgumentException(
                        $"Operation '{record.Name}' declares the mistaken name '{mistaken}', which names an operation or is declared twice.",
                        nameof(operations));
                }
            }
        }

        OperationRecord[] records = [.. ordered.Select(static operation => operation.Record)];
        _schema = new GeneratedOperationSchema(() => OperationSchemaWriter.Write(SchemaId, MaximumOperationCount, description, records), Names);
    }

    /// <summary>Canonical schema identifier of the operation document.</summary>
    public string SchemaId { get; }

    /// <summary>Largest accepted number of operations in one document.</summary>
    public int MaximumOperationCount { get; }

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
                MaximumOperationCount = MaximumOperationCount,
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

    /// <summary>
    /// The <c>OPS_INVALID</c> failure of the operation at <paramref name="index"/> for a rule a
    /// product checks against the document before the batch runs, such as two operations that
    /// change the same object. Its details name the operation as the catalog's own checks do.
    /// </summary>
    /// <param name="index">The operation's zero-based position in the batch.</param>
    /// <param name="operation">The operation at fault.</param>
    /// <param name="rejection">The rule it breaks, with its own hint, if any.</param>
    public CliException Invalid(int index, TOp operation, OperationInvalidException rejection)
    {
        ArgumentNullException.ThrowIfNull(rejection);
        return OperationErrors.InvalidAt(
            index, NameOf(operation), rejection.Message, rejection.Hint ?? DefaultHint, value: rejection.Mistake);
    }

    /// <summary>Parses, validates and identifies an operation document.</summary>
    /// <exception cref="CliException"><c>OPS_INVALID</c>, with the index of the first failing operation and, when several fail, each in <c>details.errors</c>.</exception>
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
        catch (Exception exception) when (IsRejection(exception))
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
    /// <exception cref="CliException"><c>OPS_INVALID</c>, with the index of the first failing operation and, when several fail, each in <c>details.errors</c>.</exception>
    public TBatch Prepare<TBatch>(TBatch batch)
        where TBatch : BoundedOperationEnvelope<TOp>
    {
        ArgumentNullException.ThrowIfNull(batch);
        BoundedOperationValidation.ValidateEnvelope(batch, SchemaId, Invalid);
        if (batch.Ops.Count < 1 || batch.Ops.Count > MaximumOperationCount)
        {
            throw Invalid($"the ops array must contain 1-{MaximumOperationCount} operations");
        }

        IReadOnlyList<TOp> identified = BoundedOperationIds.Assign(
            batch.Ops,
            static operation => operation.Id,
            static (operation, id) => (TOp)((BoundedOperation)operation with { Id = id }),
            Invalid);
        var validated = new TOp[identified.Count];
        var failures = new List<CliException>();
        for (int index = 0; index < identified.Count; index++)
        {
            if (Validate(index, identified[index], out validated[index]!) is { } failure)
            {
                failures.Add(failure);
            }
        }

        if (failures.Count > 0)
        {
            throw OperationErrors.InvalidAll(failures);
        }

        return (TBatch)((BoundedOperationEnvelope<TOp>)batch with { SchemaVersion = 2, Ops = validated });
    }

    /// <summary>
    /// The operation most likely meant by an entry whose op name the vocabulary does not declare,
    /// or null: the operation that declares the name a common mistake for it; else, among the
    /// names closest to it, the first whose operation accepts every field the entry gives, else
    /// the closest name. A name alone cannot tell <c>add_watermark</c> with a text field from one
    /// with an image; the fields the caller wrote can.
    /// </summary>
    /// <param name="name">The unknown op name.</param>
    /// <param name="entry">The entry that names it.</param>
    internal string? Closest(string name, JsonElement entry)
    {
        if (_byMistakenName.TryGetValue(name.Trim(), out string? meant))
        {
            return meant;
        }

        IReadOnlyList<string> closest = NameSuggestions.Closest(name, Names);
        string[] given = entry.ValueKind == JsonValueKind.Object
            ? [.. entry.EnumerateObject().Select(static field => field.Name).Where(static field => field is not ("op" or "id"))]
            : [];
        return closest.FirstOrDefault(candidate => given.All(field =>
                _byName[candidate].Record.Properties.Any(property => property.Name == field)))
            ?? closest.FirstOrDefault();
    }

    /// <summary>
    /// Checks one operation against its declared constraints, then its own
    /// <see cref="BoundedOperation.Validated"/> rules, which may normalize it.
    /// </summary>
    /// <returns>The <c>OPS_INVALID</c> failure of the operation at <paramref name="index"/>, or null when it is valid.</returns>
    private CliException? Validate(int index, TOp operation, out TOp? validated)
    {
        OperationRecord record = DescriptorOf(operation).Record;
        validated = null;
        try
        {
            OperationContractValidator.Check(record, operation);
            validated = operation.Validated() as TOp
                ?? throw new InvalidOperationException($"{operation.GetType().Name}.Validated() must return a {typeof(TOp).Name}.");
            return null;
        }
        catch (OperationInvalidException rejection)
        {
            return OperationErrors.InvalidAt(index, record.Name, rejection.Message, rejection.Hint ?? DefaultHint, value: rejection.Mistake);
        }
        catch (CliException failure) when (!failure.IsInvocationFailure)
        {
            // A shared value parser (a page range, an address) rejected a field.
            return OperationErrors.InvalidAt(index, record.Name, failure.Message, failure.Hint ?? DefaultHint, failure.Code);
        }
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
            throw Invalid($"the document is not valid JSON: {SyntaxError(exception)}");
        }
    }

    /// <summary>
    /// The reader's explanation with its position counted from one, as editors count: the
    /// reader's own message ends with zero-based line and byte numbers.
    /// </summary>
    private static string SyntaxError(JsonException exception)
    {
        int suffix = exception.Message.IndexOf(" LineNumber:", StringComparison.Ordinal);
        string reason = suffix < 0 ? exception.Message : exception.Message[..suffix];
        return exception is { LineNumber: long line, BytePositionInLine: long position }
            ? $"{reason} (line {line + 1}, byte {position + 1})"
            : reason;
    }

    /// <summary>
    /// Restates a serializer rejection for the agent. The operation converter already words its
    /// failures in wire terms and the serializer records the failing operation's position, so
    /// such a failure keeps its text and gains the index and name, and every other invalid
    /// operation is reported with it; any other failure is explained from the document
    /// against the envelope contract.
    /// </summary>
    private CliException Rejected(JsonElement root, Type batchType, JsonSerializerOptions options, JsonException? rejection)
    {
        if (OperationIndex(rejection?.Path) is { } index)
        {
            // The serializer stops at the first operation that does not fit its contract; each
            // operation of a batch within the limit is read on its own, and checked when it
            // fits, so the error names every invalid operation.
            JsonTypeInfo info = options.GetTypeInfo(typeof(TOp));
            JsonElement ops = root.GetProperty("ops");
            var failures = new List<CliException>();
            int count = ops.GetArrayLength() <= MaximumOperationCount ? ops.GetArrayLength() : 0;
            for (int current = 0; current < count; current++)
            {
                TOp operation;
                try
                {
                    operation = (TOp)ops[current].Deserialize(info)!;
                }
                catch (Exception failure) when (IsRejection(failure))
                {
                    failures.Add(InvalidAt(root, current, failure as JsonException
                        ?? JsonContractDiagnostics.Explain(ops[current], typeof(TOp), options, failurePath: null)));
                    continue;
                }

                if (Validate(current, operation, out _) is { } invalid)
                {
                    failures.Add(invalid);
                }
            }

            return OperationErrors.InvalidAll(failures.Count > 0 ? failures : [InvalidAt(root, index, rejection!)]);
        }

        JsonException reason = JsonContractDiagnostics.Explain(root, batchType, options, rejection?.Path);
        return OperationErrors.Invalid(reason.Message, DefaultHint, reason as AllowedFieldsException);
    }

    /// <summary>Whether the serializer rejected the document's content rather than failed itself.</summary>
    private static bool IsRejection(Exception exception) =>
        exception is JsonException or InvalidOperationException or NotSupportedException;

    private static int? OperationIndex(string? path)
    {
        const string Prefix = "$.ops[";
        return path is not null && path.StartsWith(Prefix, StringComparison.Ordinal) && path.EndsWith(']')
            && int.TryParse(path.AsSpan(Prefix.Length, path.Length - Prefix.Length - 1),
                NumberStyles.None, CultureInfo.InvariantCulture, out int index)
                ? index
                : null;
    }

    /// <summary>
    /// Rejects the entry at <paramref name="index"/> that the serializer could not read. An
    /// entry whose op names no operation lists the operations and the closest one.
    /// </summary>
    private CliException InvalidAt(JsonElement root, int index, JsonException failure) =>
        NameAt(root, index) is { } name && !_byName.ContainsKey(name)
            ? OperationErrors.UnknownAt(index, failure.Message, DefaultHint,
                Mistake.Of(name, Names, meant: Closest(name, root.GetProperty("ops")[index])))
            : OperationErrors.InvalidAt(
                index, KnownNameAt(root, index), failure.Message, DefaultHint, field: failure as AllowedFieldsException);

    private string? KnownNameAt(JsonElement root, int index) =>
        NameAt(root, index) is { } name && _byName.ContainsKey(name) ? name : null;

    /// <summary>The op name of the entry at <paramref name="index"/>, or null when it states none.</summary>
    private static string? NameAt(JsonElement root, int index) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty("ops", out JsonElement ops) && ops.ValueKind == JsonValueKind.Array
        && index < ops.GetArrayLength()
        && ops[index] is { ValueKind: JsonValueKind.Object } operation
        && operation.TryGetProperty("op", out JsonElement name) && name.ValueKind == JsonValueKind.String
            ? name.GetString()
            : null;

    private OperationDescriptor DescriptorOf(TOp operation) =>
        _byType.TryGetValue(operation.GetType(), out OperationDescriptor? descriptor)
            ? descriptor
            : throw new InvalidOperationException(
                $"Operation type {operation.GetType().Name} is not an operation of its catalog.");

    private CliException Invalid(string reason) => OperationErrors.Invalid(reason, DefaultHint);
}
