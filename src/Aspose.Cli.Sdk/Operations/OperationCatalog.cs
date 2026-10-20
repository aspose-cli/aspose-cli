using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
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
    private readonly Dictionary<string, string> _byMistakenName = new(StringComparer.OrdinalIgnoreCase);
    private readonly string[] _common;
    private readonly GeneratedOperationSchema _schema;

    // Writes an operation document for a command line: a placeholder such as <password-env> stays as it is.
    private static readonly JsonSerializerOptions Readable = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// Creates a vocabulary from its operation descriptors, published in ordinal order of their
    /// wire names. Only generated code calls this constructor.
    /// </summary>
    /// <param name="schemaId">
    /// The schema id of the operation document relative to the vocabulary's owner, such as
    /// <c>ops</c>: the product whose assembly declares the vocabulary.
    /// </param>
    /// <param name="description">The vocabulary's description, published on the schema.</param>
    /// <param name="contracts">Source-generated JSON metadata for every operation record.</param>
    /// <param name="operations">The operation descriptors.</param>
    public OperationCatalog(
        string schemaId,
        string? description,
        IJsonTypeInfoResolver contracts,
        IReadOnlyList<OperationDescriptor> operations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaId);
        ArgumentNullException.ThrowIfNull(contracts);
        ArgumentNullException.ThrowIfNull(operations);
        string owner = ResultEnvelope.OwnerOf(typeof(TOp).Assembly);
        SchemaId = ResultEnvelope.SchemaUri(owner, schemaId);
        Contracts = contracts;
        SchemaCommandId = ResultSchemaSet.Id(owner, schemaId);
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
        _common = (typeof(TOp).GetCustomAttributes(typeof(OperationVocabularyAttribute), inherit: false)
            .OfType<OperationVocabularyAttribute>().FirstOrDefault()?.Common) ?? [];
        if (_common.FirstOrDefault(name => !_byName.ContainsKey(name)) is { } unknown)
        {
            throw new ArgumentException($"The vocabulary's common operation '{unknown}' is not one of its operations.", nameof(operations));
        }
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
        _schema = new GeneratedOperationSchema(() => OperationSchemaWriter.Write(SchemaId, description, records), Names);
    }

    /// <summary>The canonical URI of the operation document's schema, which a document may state as its <c>schema</c>.</summary>
    public string SchemaId { get; }

    /// <summary>Largest accepted number of operations in one document, <see cref="BoundedOperation.MaximumPerBatch"/>.</summary>
    public int MaximumOperationCount => BoundedOperation.MaximumPerBatch;

    /// <summary>Wire names in published order.</summary>
    public IReadOnlyList<string> Names { get; }

    internal string DefaultHint { get; }

    /// <summary>Source-generated JSON metadata for every operation record.</summary>
    internal IJsonTypeInfoResolver Contracts { get; }

    /// <summary>The schema's id in the <c>schema</c> command, such as <c>v2/pdf/ops</c>.</summary>
    internal string SchemaCommandId { get; }

    /// <summary>
    /// Describes the command, by its path below the product, that applies documents of this
    /// vocabulary; the vocabulary's schema travels with it to the product resources.
    /// </summary>
    internal ProductOperationCommand Describe(string command)
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
            },
            _schema);
    }

    /// <summary>
    /// The operation document that encrypts the edited document: the
    /// <see cref="EncryptsOutputAttribute"/> operation with its secret fields, every one when
    /// <paramref name="openPassword"/> and otherwise only those it requires, so that the document
    /// keeps opening without a password where the operation allows it; and the
    /// <paramref name="restrictions"/> fields. Each secret field names <paramref name="variable"/>,
    /// or when it is null a placeholder of its own, such as <c>&lt;owner-password-env&gt;</c>.
    /// Null when the vocabulary has no such operation, or it requires a field that is not a secret.
    /// </summary>
    internal string? EncryptingDocument(string? variable, bool openPassword, JsonObject? restrictions)
    {
        if (_byName.Values.Select(static operation => operation.Record).FirstOrDefault(static record => record.EncryptsOutput) is not { } record
            || record.Properties.Any(static property => property.Required && !IsSecret(property)))
        {
            return null;
        }

        var operation = new JsonObject { ["op"] = record.Name };
        foreach (OperationProperty property in record.Properties.Where(property => IsSecret(property) && (openPassword || property.Required)))
        {
            operation[property.Name] = variable ?? Placeholder(property.Name);
        }

        foreach ((string name, JsonNode? value) in restrictions ?? [])
        {
            operation[name] = value?.DeepClone();
        }

        return new JsonObject { ["ops"] = new JsonArray(operation) }.ToJsonString(Readable);

        static bool IsSecret(OperationProperty property) => property.Constraints.OfType<SecretEnvAttribute>().Any();

        // ownerPasswordEnv: <owner-password-env>.
        static string Placeholder(string name) =>
            "<" + string.Concat(name.Select(static character =>
                char.IsUpper(character) ? "-" + char.ToLowerInvariant(character) : character.ToString())) + ">";
    }

    /// <summary>Returns the wire name of an operation of this vocabulary.</summary>
    public string NameOf(TOp operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return DescriptorOf(operation).Record.Name;
    }

    /// <summary>
    /// The <c>OP_NO_EFFECT</c> warning of an operation that applied but changed nothing; its
    /// details name the operation by <c>id</c> and <c>op</c>, as its outcome does, and give
    /// <paramref name="reason"/>.
    /// </summary>
    /// <param name="operation">The operation, with the id the batch gave it.</param>
    /// <param name="reason">Why it changed nothing, one of <see cref="NoEffectReasons"/>.</param>
    /// <param name="message">What the operation did not find; it never repeats a value the caller may keep private, such as find text.</param>
    public Warning NoEffect(TOp operation, string reason, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        string id = operation?.Id ?? throw new ArgumentException("The operation has no id; warn about operations of a prepared batch.", nameof(operation));
        return new Warning(WarningCodes.OpNoEffect, message)
        {
            Details = new JsonObject { ["id"] = id, ["op"] = NameOf(operation), ["reason"] = reason },
        };
    }

    /// <summary>
    /// Parses, validates and identifies an operation document: an object with <c>ops</c>, or
    /// the bare array of operations.
    /// </summary>
    /// <exception cref="CliException"><c>OPS_INVALID</c>, with the id of the first failing operation and, when several fail, each in <c>details.errors</c>.</exception>
    public TBatch Parse<TBatch>(string json, ProductJsonDefinition contracts)
        where TBatch : BoundedOperationEnvelope<TOp> =>
        Parse<TBatch>(json, contracts, quotesContent: true);

    /// <summary>
    /// Parses a document as <see cref="Parse{TBatch}(string, ProductJsonDefinition)"/> does.
    /// Without <paramref name="quotesContent"/>, a syntax error states only where the document
    /// breaks, not the text there, as for a document read from standard input, which may hold
    /// something else by mistake, such as a password.
    /// </summary>
    internal TBatch Parse<TBatch>(string json, ProductJsonDefinition contracts, bool quotesContent)
        where TBatch : BoundedOperationEnvelope<TOp>
    {
        ArgumentNullException.ThrowIfNull(contracts);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw OperationErrors.Invalid(OpsInvalidReasons.InvalidJson, "the document is empty", DefaultHint);
        }

        JsonDocument document = ParseJson(json, quotesContent);
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            // The bare array is the document with only its operations.
            document.Dispose();
            json = $"{{\"ops\":{json}}}";
            document = JsonDocument.Parse(json);
        }

        using (document)
        {
            return Prepare(Deserialize<TBatch>(json, document.RootElement, contracts));
        }
    }

    private TBatch Deserialize<TBatch>(string json, JsonElement root, ProductJsonDefinition contracts)
        where TBatch : BoundedOperationEnvelope<TOp>
    {
        TBatch batch;
        try
        {
            batch = contracts.Deserialize<TBatch>(json);
        }
        catch (Exception exception) when (IsRejection(exception))
        {
            throw Rejected(root, typeof(TBatch), contracts.LocalOptions, exception as JsonException);
        }

        return batch;
    }

    /// <summary>
    /// Validates a parsed or composed document once and assigns deterministic ids to
    /// operations without one. Each operation is checked against its declared constraints,
    /// then its own <see cref="BoundedOperation.Validated"/> rules, which may normalize it.
    /// </summary>
    /// <exception cref="CliException"><c>OPS_INVALID</c>, with the id of the first failing operation and, when several fail, each in <c>details.errors</c>.</exception>
    public TBatch Prepare<TBatch>(TBatch batch)
        where TBatch : BoundedOperationEnvelope<TOp>
    {
        ArgumentNullException.ThrowIfNull(batch);
        BoundedOperationValidation.ValidateEnvelope(batch, SchemaId, Invalid);
        if (batch.Ops.Count < 1 || batch.Ops.Count > MaximumOperationCount)
        {
            throw OperationErrors.Invalid(OpsInvalidReasons.InvalidValue, $"the ops array must contain 1-{MaximumOperationCount} operations", DefaultHint, field: "ops");
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
            if (Validate(identified[index].Id!, identified[index], out validated[index]!) is { } failure)
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
    /// the closest name. A name alone cannot tell <c>set_watermark</c> with a text field from one
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
    /// <returns>The <c>OPS_INVALID</c> failure of the operation with <paramref name="id"/>, or null when it is valid.</returns>
    private CliException? Validate(string id, TOp operation, out TOp? validated)
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
            return OperationErrors.InvalidAt(id, record.Name, rejection, DefaultHint);
        }
        catch (CliException failure) when (!failure.IsInvocationFailure)
        {
            // A shared value parser (a page range, an address) rejected a field.
            return OperationErrors.InvalidAt(id, record.Name, OpsInvalidReasons.InvalidValue, failure.Message, failure.Hint ?? DefaultHint, cause: failure.Code);
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

    /// <summary>The files an operation reads, as its <see cref="InputPathAttribute"/> members state them.</summary>
    internal IReadOnlyList<string> InputPaths(TOp operation)
    {
        var paths = new List<string>();
        _ = ResolveInputPaths(operation, path =>
        {
            paths.Add(path);
            return path;
        });
        return paths;
    }

    /// <summary>The environment variables whose secrets an operation reads; null entries are omitted fields.</summary>
    internal IEnumerable<string?> SecretVariables(TOp operation) =>
        DescriptorOf(operation).SecretVariables?.Invoke(operation) ?? [];

    private static JsonDocument ParseJson(string json, bool quotesContent)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw OperationErrors.Invalid(OpsInvalidReasons.InvalidJson, $"the document is not valid JSON{SyntaxError(exception, quotesContent)}", SyntaxHint(exception));
        }
    }

    /// <summary>
    /// The hint for a document that is not JSON, which names no operation: fix the syntax where
    /// the reason says, and, where a string holds a backslash JSON cannot hold, as a regular
    /// expression often does, write the backslash as two.
    /// </summary>
    private static string SyntaxHint(JsonException exception) =>
        exception.Message.Contains("escap", StringComparison.OrdinalIgnoreCase)
            ? "Fix the JSON syntax at the line and byte the reason names: inside a JSON string a backslash is written \\\\, "
                + "so the regular expression \\d is written \"\\\\d\" and \\+ is written \"\\\\+\"."
            : TrailingComma(exception)
                ? $"Fix the JSON syntax at the line and byte the reason names: {DeleteTrailingComma}."
                : "Fix the JSON syntax at the line and byte the reason names.";

    private const string DeleteTrailingComma = "delete the comma before the closing ']' or '}'";

    private static bool TrailingComma(JsonException exception) =>
        exception.Message.Contains("trailing comma", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The reader's explanation in the document's terms: a trailing comma says which comma to
    /// delete, and no sentence speaks of the reader's own options or modes, which the caller
    /// cannot change.
    /// </summary>
    private static string SyntaxReason(JsonException exception, string reason)
    {
        if (TrailingComma(exception))
        {
            return $"a comma ends a list or an object; {DeleteTrailingComma}";
        }

        string[] sentences = [.. reason.Split(". ", StringSplitOptions.RemoveEmptyEntries)
            .Where(static sentence => !sentence.Contains("option", StringComparison.OrdinalIgnoreCase)
                && !sentence.Contains("in this mode", StringComparison.OrdinalIgnoreCase))];
        return sentences.Length == 0 ? "the JSON syntax is invalid" : string.Join(". ", sentences);
    }

    /// <summary>
    /// The reader's explanation with its position counted from one, as editors count: the
    /// reader's own message ends with zero-based line and byte numbers. Without
    /// <paramref name="quotesContent"/> only the position, since the reader's explanation can
    /// quote the text it stopped at.
    /// </summary>
    private static string SyntaxError(JsonException exception, bool quotesContent)
    {
        int suffix = exception.Message.IndexOf(" LineNumber:", StringComparison.Ordinal);
        string reason = suffix < 0 ? exception.Message : exception.Message[..suffix];
        string position = exception is { LineNumber: long line, BytePositionInLine: long column }
            ? $" (line {line + 1}, byte {column + 1})"
            : string.Empty;
        return quotesContent ? $": {SyntaxReason(exception, reason)}{position}" : position;
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
            IReadOnlyList<string> ids = BoundedOperationIds.Of(ops);
            var failures = new List<CliException>();
            int count = ops.GetArrayLength() <= MaximumOperationCount ? ops.GetArrayLength() : 0;
            for (int current = 0; current < count; current++)
            {
                TOp operation;
                try
                {
                    operation = (TOp)ops[current].Deserialize(info)!;
                }
                catch (UnknownFieldsException unknown)
                {
                    failures.AddRange(unknown.Fields.Select(field => InvalidAt(ops, ids, current, field)));
                    continue;
                }
                catch (Exception failure) when (IsRejection(failure))
                {
                    failures.Add(InvalidAt(ops, ids, current, failure as JsonException
                        ?? JsonContractDiagnostics.Explain(ops[current], typeof(TOp), options, failurePath: null)));
                    continue;
                }

                if (Validate(ids[current], operation, out _) is { } invalid)
                {
                    failures.Add(invalid);
                }
            }

            return OperationErrors.InvalidAll(failures.Count > 0 ? failures : [InvalidAt(ops, ids, index, rejection!)]);
        }

        JsonException reason = JsonContractDiagnostics.Explain(root, batchType, options, rejection?.Path);
        return OperationErrors.Invalid(reason, DefaultHint);
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
    /// Rejects the entry at <paramref name="index"/> of <paramref name="ops"/> that the
    /// serializer could not read, by the id <paramref name="ids"/> gives it. An entry whose op
    /// names no operation lists the operations and the closest one.
    /// </summary>
    private CliException InvalidAt(JsonElement ops, IReadOnlyList<string> ids, int index, JsonException failure) =>
        NameAt(ops, index) is { } name && !_byName.ContainsKey(name)
            ? OperationErrors.UnknownAt(ids[index], failure.Message, DefaultHint,
                Mistake.Of(name, ByRelevance(name), meant: Closest(name, ops[index])))
            : OperationErrors.InvalidAt(
                ids[index], KnownNameAt(ops, index), failure, KindHint(KnownNameAt(ops, index), failure) ?? HintFor(KnownNameAt(ops, index)));

    /// <summary>
    /// The operation names in the order an error for the unknown <paramref name="name"/> lists
    /// them: the closest names, then the vocabulary's common operations, then the names that share
    /// a word with it, such as its verb or its noun, then the rest in published order.
    /// </summary>
    private IEnumerable<string> ByRelevance(string name)
    {
        string[] words = name.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return NameSuggestions.Closest(name, Names)
            .Concat(_common)
            .Concat(Names.Where(candidate => candidate.Split('_').Intersect(words, StringComparer.OrdinalIgnoreCase).Any()))
            .Concat(Names)
            .Distinct(StringComparer.Ordinal);
    }

    /// <summary>
    /// The hint of an entry the serializer could not read: the operation's own
    /// (<see cref="OperationAttribute.Hint"/>) before the default.
    /// </summary>
    private string HintFor(string? name) =>
        name is not null && _byName[name].Record.Type.GetCustomAttributes(typeof(OperationAttribute), inherit: false)
            .OfType<OperationAttribute>().FirstOrDefault()?.Hint is { } hint
            ? $"{hint} {DefaultHint}"
            : DefaultHint;

    /// <summary>
    /// For a field of an operation whose value has the wrong kind, what the field is, from its
    /// description, before the operation's hint (<see cref="HintFor"/>): an <c>at</c> that is a
    /// section number in one operation and a block address in another is otherwise only "must be
    /// a whole number".
    /// </summary>
    private string? KindHint(string? name, JsonException failure)
    {
        if (failure is not ValueKindException { Field: var field } || name is null
            || _byName[name].Record.Properties.FirstOrDefault(property => property.Name == field) is not { Description: { Length: > 0 } description })
        {
            return null;
        }

        string meaning = description.StartsWith("The ", StringComparison.Ordinal) || description.StartsWith("A ", StringComparison.Ordinal) || description.StartsWith("An ", StringComparison.Ordinal)
            ? $"'{field}' is {char.ToLowerInvariant(description[0])}{description[1..]}"
            : $"'{field}': {description}";
        return $"{meaning.TrimEnd('.')}. {HintFor(name)}";
    }

    private string? KnownNameAt(JsonElement ops, int index) =>
        NameAt(ops, index) is { } name && _byName.ContainsKey(name) ? name : null;

    /// <summary>The op name of the entry at <paramref name="index"/>, or null when it states none.</summary>
    private static string? NameAt(JsonElement ops, int index) =>
        index < ops.GetArrayLength()
        && ops[index] is { ValueKind: JsonValueKind.Object } operation
        && operation.TryGetProperty("op", out JsonElement name) && name.ValueKind == JsonValueKind.String
            ? name.GetString()
            : null;

    private OperationDescriptor DescriptorOf(TOp operation) =>
        _byType.TryGetValue(operation.GetType(), out OperationDescriptor? descriptor)
            ? descriptor
            : throw new InvalidOperationException(
                $"Operation type {operation.GetType().Name} is not an operation of its catalog.");

    /// <summary>Rejects a value of the document's envelope or of an operation id, such as a duplicated id.</summary>
    private CliException Invalid(string explanation) => OperationErrors.Invalid(OpsInvalidReasons.InvalidValue, explanation, DefaultHint);
}
