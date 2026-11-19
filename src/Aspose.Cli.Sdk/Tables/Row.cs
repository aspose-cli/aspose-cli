using System.Drawing;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aspose.Cli.Sdk.Tables;

/// <summary>
/// One row of the object table: a name for one engine member. A read gets the member and a set
/// sets it, under the same name, and nothing else; a read-only row has no setter.
/// </summary>
public abstract class TableRow
{
    private protected TableRow(string name, ValueKind kind, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Name = name;
        Kind = kind;
        Description = description;
    }

    /// <summary>The row's name, such as <c>fill.color</c>.</summary>
    public string Name { get; }

    /// <summary>How the row's value is written.</summary>
    public ValueKind Kind { get; }

    /// <summary>The engine member the row names, written into the schema.</summary>
    public string Description { get; }

    /// <summary>Whether the row can be set.</summary>
    public abstract bool Settable { get; }

    /// <summary>The engine type whose objects have the row.</summary>
    public abstract Type Target { get; }

    /// <summary>Whether <paramref name="item"/> has the row.</summary>
    internal bool AppliesTo(object item) => Target.IsInstanceOfType(item);

    /// <summary>Gets the member of <paramref name="item"/>, written in the row's kind.</summary>
    internal abstract JsonNode? Read(object item);

    /// <summary>Reads a value the caller wrote for the row.</summary>
    /// <exception cref="TableValueException">The value is not of the row's kind.</exception>
    internal object? Decode(JsonElement value) => Kind.DecodeObject(value);

    /// <summary>Sets the member of <paramref name="item"/> to a value <see cref="Decode"/> read.</summary>
    internal abstract void Write(object item, object? value);
}

/// <summary>A row of objects of type <typeparamref name="T"/>.</summary>
public sealed class Row<T> : TableRow
    where T : class
{
    private readonly Func<T, object?> _get;
    private readonly Action<T, object?>? _set;

    internal Row(string name, ValueKind kind, string description, Func<T, object?> get, Action<T, object?>? set)
        : base(name, kind, description)
    {
        _get = get ?? throw new ArgumentNullException(nameof(get));
        _set = set;
    }

    /// <inheritdoc />
    public override bool Settable => _set is not null;

    /// <inheritdoc />
    public override Type Target => typeof(T);

    /// <summary>
    /// The same row on objects of <typeparamref name="TOuter"/> that reach a <typeparamref name="T"/>
    /// through <paramref name="path"/>, named <c>prefix.name</c>, such as <c>fill.color</c> on a shape.
    /// </summary>
    public Row<TOuter> Under<TOuter>(string prefix, Func<TOuter, T> path)
        where TOuter : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentNullException.ThrowIfNull(path);
        Action<T, object?>? set = _set;
        return new Row<TOuter>(
            $"{prefix}.{Name}",
            Kind,
            Description,
            outer => _get(path(outer)),
            set is null ? null : (outer, value) => set(path(outer), value));
    }

    internal override JsonNode? Read(object item) => Kind.EncodeObject(_get((T)item));

    internal override void Write(object item, object? value)
    {
        if (_set is null)
        {
            throw new InvalidOperationException($"Row '{Name}' is read-only.");
        }

        _set((T)item, value);
    }
}

/// <summary>The rows of the object table, one factory per value kind.</summary>
public static class Row
{
    /// <summary>A row of any value kind; <paramref name="set"/> is null for a read-only row.</summary>
    public static Row<T> Of<T, TValue>(string name, ValueKind<TValue> kind, string description, Func<T, TValue> get, Action<T, TValue>? set = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(get);
        return new Row<T>(name, kind, description, item => get(item), set is null ? null : (item, value) => set(item, (TValue)value!));
    }

    /// <summary>A text row.</summary>
    public static Row<T> Text<T>(string name, string description, Func<T, string?> get, Action<T, string>? set = null)
        where T : class =>
        Of(name, ValueKinds.Text, description, get, set is null ? null : (item, value) => set(item, value!));

    /// <summary>A whole-number row.</summary>
    public static Row<T> Integer<T>(string name, string description, Func<T, int> get, Action<T, int>? set = null)
        where T : class => Of(name, ValueKinds.Integer, description, get, set);

    /// <summary>An unsigned whole-number row, such as an engine id.</summary>
    public static Row<T> UnsignedInteger<T>(string name, string description, Func<T, uint> get, Action<T, uint>? set = null)
        where T : class => Of(name, ValueKinds.UnsignedInteger, description, get, set);

    /// <summary>A number row.</summary>
    public static Row<T> Number<T>(string name, string description, Func<T, double> get, Action<T, double>? set = null)
        where T : class => Of(name, ValueKinds.Number, description, get, set);

    /// <summary>A row of the engine's single-precision numbers that are no length.</summary>
    public static Row<T> Single<T>(string name, string description, Func<T, float> get, Action<T, float>? set = null)
        where T : class => Of(name, ValueKinds.Single, description, get, set);

    /// <summary>A length row, in points.</summary>
    public static Row<T> Length<T>(string name, string description, Func<T, float> get, Action<T, float>? set = null)
        where T : class => Of(name, ValueKinds.Length, description, get, set);

    /// <summary>A true-or-false row.</summary>
    public static Row<T> Boolean<T>(string name, string description, Func<T, bool> get, Action<T, bool>? set = null)
        where T : class => Of(name, ValueKinds.Boolean, description, get, set);

    /// <summary>A color row, written <c>#RRGGBB</c>.</summary>
    public static Row<T> Color<T>(string name, string description, Func<T, Color> get, Action<T, Color>? set = null)
        where T : class => Of(name, ValueKinds.Color, description, get, set);

    /// <summary>A GUID row.</summary>
    public static Row<T> Guid<T>(string name, string description, Func<T, Guid> get)
        where T : class => Of(name, ValueKinds.Guid, description, get);

    /// <summary>A row whose values are the members of the engine's enumeration <typeparamref name="TEnum"/>.</summary>
    public static Row<T> Enum<T, TEnum>(string name, string description, Func<T, TEnum> get, Action<T, TEnum>? set = null)
        where T : class
        where TEnum : struct, System.Enum => Of(name, ValueKinds.Enum<TEnum>(), description, get, set);
}
