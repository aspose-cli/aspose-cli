using System.Globalization;
using System.Text.Json;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>Product-neutral validation for the bounded operation envelope.</summary>
public static class BoundedOperationValidation
{
    private static readonly IReadOnlySet<string> EnvelopeProperties =
        new HashSet<string>(
            ["schema", "schemaVersion", "ifMatch", "ops"],
            StringComparer.Ordinal);

    /// <summary>
    /// Validates the shared JSON envelope and delegates the operation field
    /// roster and error construction to the owning product.
    /// </summary>
    public static void ValidateJsonShape(
        JsonElement root,
        IReadOnlyDictionary<string, IReadOnlySet<string>> allowedPropertiesByOperation,
        int maximumOperations,
        Func<string, Exception> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(allowedPropertiesByOperation);
        ArgumentNullException.ThrowIfNull(errorFactory);
        if (maximumOperations < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumOperations),
                maximumOperations,
                "The maximum operation count must be positive.");
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw errorFactory("the operations document must be a JSON object");
        }

        var rootNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (!rootNames.Add(property.Name))
            {
                throw errorFactory(
                    $"root property '{property.Name}' is duplicated");
            }
            if (!EnvelopeProperties.Contains(property.Name))
            {
                throw errorFactory(
                    $"root property '{property.Name}' is not supported");
            }
        }

        if (!root.TryGetProperty("ops", out JsonElement operations)
            || operations.ValueKind != JsonValueKind.Array)
        {
            throw errorFactory("ops must be a JSON array");
        }

        if (operations.GetArrayLength() is < 1 || operations.GetArrayLength() > maximumOperations)
        {
            throw errorFactory(
                $"ops must contain 1-{maximumOperations} operations");
        }

        int index = 0;
        foreach (JsonElement operation in operations.EnumerateArray())
        {
            if (operation.ValueKind != JsonValueKind.Object)
            {
                throw errorFactory(
                    $"ops[{index}] must be an object with a string op");
            }

            var operationNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in operation.EnumerateObject())
            {
                if (!operationNames.Add(property.Name))
                {
                    throw errorFactory(
                        $"ops[{index}] property '{property.Name}' is duplicated");
                }

                BoundedJsonValidation.ValidateNoDuplicateProperties(
                    property.Value,
                    $"ops[{index}].{property.Name}",
                    errorFactory);
            }

            if (!operation.TryGetProperty("op", out JsonElement discriminator)
                || discriminator.ValueKind != JsonValueKind.String)
            {
                throw errorFactory(
                    $"ops[{index}] must be an object with a string op");
            }

            string name = discriminator.GetString()!;
            if (!allowedPropertiesByOperation.TryGetValue(
                    name,
                    out IReadOnlySet<string>? allowed))
            {
                throw errorFactory(
                    $"ops[{index}] operation '{name}' is not supported by this build");
            }

            foreach (JsonProperty property in operation.EnumerateObject())
            {
                if (!allowed.Contains(property.Name))
                {
                    throw errorFactory(
                        $"ops[{index}] '{name}' property '{property.Name}' is not supported");
                }
            }

            index++;
        }
    }

    /// <summary>Validates the shared version and optional schema identity once.</summary>
    public static void ValidateEnvelope<TOperation>(
        BoundedOperationEnvelope<TOperation> envelope,
        string expectedSchema,
        Func<string, Exception> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSchema);
        ArgumentNullException.ThrowIfNull(errorFactory);
        if (envelope.SchemaVersion is not null and not 2)
        {
            throw errorFactory(
                $"unsupported schemaVersion {envelope.SchemaVersion}; this build supports version 2");
        }
        if (envelope.Schema is not null
            && !string.Equals(
                envelope.Schema,
                expectedSchema,
                StringComparison.Ordinal))
        {
            throw errorFactory(
                $"schema must be '{expectedSchema}' when present");
        }
    }

}

/// <summary>Stable identifiers shared by bounded operation documents.</summary>
public static class BoundedOperationIds
{
    /// <summary>Returns whether an explicit operation ID follows the wire grammar.</summary>
    public static bool IsValid(string? id) =>
        id is { Length: >= 1 and <= 64 }
        && char.IsAsciiLetter(id[0])
        && id.All(static character =>
            char.IsAsciiLetterOrDigit(character)
            || character is '.' or '_' or '-');

    /// <summary>Creates the deterministic default ID for a zero-based operation index.</summary>
    public static string CreateDefault(int index)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, null);
        }

        return $"op-{(index + 1).ToString("D4", CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// Allocates and reserves the first default ID at or after a zero-based
    /// operation index that is not already present.
    /// </summary>
    public static string Allocate(int index, ISet<string> used)
    {
        ArgumentNullException.ThrowIfNull(used);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, null);
        }

        int candidate = index;
        while (true)
        {
            string id = CreateDefault(candidate);
            if (used.Add(id))
            {
                return id;
            }

            candidate = checked(candidate + 1);
        }
    }

    /// <summary>
    /// Validates explicit IDs and deterministically assigns every omitted ID.
    /// Product-specific operation validation remains with the caller.
    /// </summary>
    public static IReadOnlyList<TOperation> Assign<TOperation>(
        IReadOnlyList<TOperation> operations,
        Func<TOperation, string?> getId,
        Func<TOperation, string, TOperation> withId,
        Func<string, Exception> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(getId);
        ArgumentNullException.ThrowIfNull(withId);
        ArgumentNullException.ThrowIfNull(errorFactory);

        var used = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < operations.Count; index++)
        {
            TOperation operation = operations[index];
            if (operation is null)
            {
                throw errorFactory($"ops[{index}] is null");
            }
            string? id = getId(operation);
            if (id is null)
            {
                continue;
            }
            if (!IsValid(id))
            {
                throw errorFactory(
                    $"operation id '{id}' must start with a letter and contain at most 64 letters, digits, '.', '_' or '-'");
            }
            if (!used.Add(id))
            {
                throw errorFactory($"operation id '{id}' is duplicated");
            }
        }

        var assigned = new TOperation[operations.Count];
        for (int index = 0; index < operations.Count; index++)
        {
            TOperation operation = operations[index];
            string id = getId(operation) ?? Allocate(index, used);
            assigned[index] = withId(operation, id);
        }

        return assigned;
    }

}
