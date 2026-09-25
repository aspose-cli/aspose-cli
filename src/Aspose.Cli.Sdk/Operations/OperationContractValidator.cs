using System.Collections;

namespace Aspose.Cli.Sdk.Operations;

/// <summary>A constraint and how many array or map levels below the current value it still has to descend.</summary>
internal readonly record struct PlacedConstraint(ValueConstraintAttribute Constraint, int Depth)
{
    /// <summary>A property's declared constraints, placed at their declared depths.</summary>
    public static PlacedConstraint[] Declared(IReadOnlyList<ValueConstraintAttribute> constraints) =>
        [.. constraints.Select(static constraint => new PlacedConstraint(constraint, constraint.Depth))];
}

/// <summary>
/// Enforces the declared constraints of an operation, recursively through its nested records,
/// arrays and maps. Failures name the value by its wire path, such as <c>rect.width</c>.
/// </summary>
internal static class OperationContractValidator
{
    /// <summary>Checks an operation against its record's constraints.</summary>
    /// <exception cref="OperationInvalidException">A value breaks a constraint.</exception>
    public static void Check(OperationRecord record, object operation) => CheckRecord(record, operation, string.Empty);

    /// <summary>
    /// Splits the constraints that reach a value into those that apply to the value itself and
    /// those that apply to the items of its arrays and maps; the operation generator placed
    /// each at its level (see <see cref="ValueConstraintAttribute"/>).
    /// </summary>
    public static (IReadOnlyList<ValueConstraintAttribute> Here, IReadOnlyList<PlacedConstraint> Items) Place(
        OperationValue value,
        IReadOnlyList<PlacedConstraint> constraints,
        string path)
    {
        ValueConstraintAttribute[] here = [.. constraints.Where(static placed => placed.Depth == 0).Select(static placed => placed.Constraint)];
        PlacedConstraint[] items = [.. constraints.Where(static placed => placed.Depth > 0).Select(static placed => placed with { Depth = placed.Depth - 1 })];
        if (items.Length > 0 && value.Kind is not (OperationValueKind.Array or OperationValueKind.Map))
        {
            // The generator never places a constraint below a value that has no items.
            throw new InvalidOperationException($"Constraint {items[0].Constraint.GetType().Name} on '{path}' is placed below a {value.Kind} value.");
        }

        return (here, items);
    }

    private static void CheckRecord(OperationRecord record, object instance, string path)
    {
        var set = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (OperationProperty property in record.Properties)
        {
            object? value = property.Get(instance);
            if (value is null)
            {
                continue;
            }

            // See RecordRuleAttribute: a Boolean that is not nullable is set only when true.
            if (!(value is false && property.Value is { Kind: OperationValueKind.Boolean, Nullable: false }))
            {
                set.Add(property.Name, value);
            }

            CheckValue(property.Value, PlacedConstraint.Declared(property.Constraints), value, Join(path, property.Name));
        }

        foreach (ValueConstraintAttribute constraint in record.Constraints)
        {
            if (constraint.Check(set) is { } reason)
            {
                throw new OperationInvalidException($"{(path.Length == 0 ? "the operation" : path)} {reason}");
            }
        }
    }

    private static void CheckValue(OperationValue shape, IReadOnlyList<PlacedConstraint> constraints, object value, string path)
    {
        (IReadOnlyList<ValueConstraintAttribute> here, IReadOnlyList<PlacedConstraint> items) = Place(shape, constraints, path);
        foreach (ValueConstraintAttribute constraint in here)
        {
            if (constraint.Check(value) is { } reason)
            {
                throw new OperationInvalidException($"{path} {reason}");
            }
        }

        switch (shape.Kind)
        {
            case OperationValueKind.Record:
                CheckRecord(shape.Record!, value, path);
                break;
            case OperationValueKind.Array:
                int index = 0;
                foreach (object? item in (IEnumerable)value)
                {
                    if (item is not null)
                    {
                        CheckValue(shape.Items!, items, item, $"{path}[{index}]");
                    }

                    index++;
                }

                break;
            case OperationValueKind.Map:
                foreach (DictionaryEntry entry in (IDictionary)value)
                {
                    if (entry.Value is not null)
                    {
                        CheckValue(shape.Items!, items, entry.Value, Join(path, (string)entry.Key));
                    }
                }

                break;
        }
    }

    internal static string Join(string path, string name) => path.Length == 0 ? name : $"{path}.{name}";
}
