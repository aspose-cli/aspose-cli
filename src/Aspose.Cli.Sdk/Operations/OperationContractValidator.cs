using System.Collections;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;

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

            CheckValue(property.Value, PlacedConstraint.Declared(property.Constraints), value, Join(path, property.Name), AnyColor(record, path));
        }

        foreach (RecordRuleAttribute constraint in record.Constraints)
        {
            if (constraint.Check(set) is { } reason)
            {
                throw new OperationInvalidException($"{(path.Length == 0 ? "the operation" : path)} {reason}");
            }
        }
    }

    /// <summary>A value without its word separators, so <c>very-hidden</c> and <c>veryHidden</c> compare alike.</summary>
    private static string Bare(string value) => value.Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal);

    /// <summary>The wire path of the record's member that takes any <c>#RRGGBB</c> color, or null for none.</summary>
    private static string? AnyColor(OperationRecord record, string path) =>
        record.Properties.FirstOrDefault(static property => property.Constraints.OfType<HexColorAttribute>().Any()) is { } color
            ? Join(path, color.Name)
            : null;

    private static void CheckValue(OperationValue shape, IReadOnlyList<PlacedConstraint> constraints, object value, string path, string? anyColor = null)
    {
        (IReadOnlyList<ValueConstraintAttribute> here, IReadOnlyList<PlacedConstraint> items) = Place(shape, constraints, path);
        foreach (ValueConstraintAttribute constraint in here)
        {
            if (constraint.Check(value) is { } reason)
            {
                throw constraint is AllowedValuesAttribute allowed && value is string text
                    ? NotAllowed(allowed, text, path, anyColor)
                    : new OperationInvalidException($"{path} {reason}");
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

    /// <summary>
    /// A name among fixed values is a mistake: the operation error lists the values once, in
    /// details.available and the message, so the reason leaves them out. A value spelled with
    /// other case or separators, such as veryHidden for very-hidden, is meant first; a
    /// <c>#RRGGBB</c> color given for a palette's color names means the closest of them, and the
    /// hint names the member of the record that takes any color, <paramref name="anyColor"/>.
    /// </summary>
    private static OperationInvalidException NotAllowed(AllowedValuesAttribute allowed, string text, string path, string? anyColor)
    {
        string[] spelled = [.. allowed.Spelled.Where(spelled => string.Equals(Bare(spelled), Bare(text), StringComparison.OrdinalIgnoreCase))];
        string? color = spelled.Length == 0 ? allowed.ClosestColor(text) : null;
        string? hint = color is null
            ? null
            : $"'{path}' takes only the named colors{(anyColor is null ? "." : $"; '{anyColor}' takes any #RRGGBB color.")}";
        return new OperationInvalidException(
            $"{path} does not accept '{text}'",
            hint,
            Mistake.Of(text, allowed.Spelled, color is null ? spelled : [color]));
    }

    internal static string Join(string path, string name) => path.Length == 0 ? name : $"{path}.{name}";
}
