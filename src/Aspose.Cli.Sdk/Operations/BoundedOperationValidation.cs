using System.Globalization;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Operations;

/// <summary>Product-neutral validation for the bounded operation envelope.</summary>
internal static class BoundedOperationValidation
{
    /// <summary>Validates the shared version and optional schema identity once.</summary>
    public static void ValidateEnvelope<TOperation>(
        BoundedOperationEnvelope<TOperation> envelope,
        string expectedSchema,
        Func<string, Exception> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSchema);
        ArgumentNullException.ThrowIfNull(errorFactory);
        if (envelope.Ops is null)
        {
            throw errorFactory("ops must be a JSON array");
        }
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
internal static class BoundedOperationIds
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
