using System.Collections;
using System.Globalization;
using System.Text.Json.Nodes;

namespace Aspose.Cli.Sdk.Tables;

/// <summary>
/// One kind of object of a document, such as a slide or a shape: its rows. Where it sits, how it
/// is found, added, deleted and moved is declared on the <see cref="DocumentTable{TDocument}"/>.
/// </summary>
public abstract class ObjectKind
{
    private readonly List<TableRow> _rows = [];

    private protected ObjectKind(string name, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Name = name;
        Description = description;
    }

    /// <summary>The kind's name, such as <c>shape</c>.</summary>
    public string Name { get; }

    /// <summary>What the kind is, written into the schema.</summary>
    public string Description { get; }

    /// <summary>The engine type of the kind's objects.</summary>
    public abstract Type Type { get; }

    /// <summary>The kind's rows in declaration order.</summary>
    public IReadOnlyList<TableRow> Rows => _rows;

    private protected void AddRow(TableRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (_rows.Any(existing => existing.Name == row.Name && (existing.Target.IsAssignableFrom(row.Target) || row.Target.IsAssignableFrom(existing.Target))))
        {
            throw new ArgumentException($"Kind '{Name}' declares row '{row.Name}' twice.", nameof(row));
        }

        if (_rows.Any(existing => existing.Name == row.Name && existing.Kind.Name != row.Kind.Name))
        {
            throw new ArgumentException($"Kind '{Name}' declares row '{row.Name}' with two value kinds.", nameof(row));
        }

        _rows.Add(row);
    }

    /// <summary>The rows <paramref name="item"/> has, in declaration order.</summary>
    internal IEnumerable<TableRow> RowsOf(object item) => _rows.Where(row => row.AppliesTo(item));

    /// <summary>The names of the settable rows any object of the kind can have.</summary>
    internal IReadOnlyList<string> SettableNames =>
        [.. _rows.Where(static row => row.Settable).Select(static row => row.Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    /// <summary>Reads every row <paramref name="item"/> has.</summary>
    /// <exception cref="RowReadException">The engine threw while one row was read.</exception>
    internal JsonObject Read(object item)
    {
        var props = new JsonObject();
        foreach (TableRow row in RowsOf(item))
        {
            JsonNode? value;
            try
            {
                value = row.Read(item);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new RowReadException(row.Name, exception);
            }

            props[row.Name] = value;
        }

        return props;
    }
}

/// <summary>The engine threw while a row was read; the caller reports it with the object's address.</summary>
internal sealed class RowReadException(string row, Exception inner) : Exception(inner.Message, inner)
{
    /// <summary>The row whose engine member threw.</summary>
    public string Row { get; } = row;
}

/// <summary>A kind whose objects are engine objects of type <typeparamref name="T"/>; its rows are listed with a collection initializer.</summary>
public sealed class ObjectKind<T> : ObjectKind, IEnumerable<TableRow>
    where T : class
{
    /// <summary>Declares a kind.</summary>
    public ObjectKind(string name, string description)
        : base(name, description)
    {
    }

    /// <inheritdoc />
    public override Type Type => typeof(T);

    /// <summary>Adds a row every object of the kind has.</summary>
    public void Add(Row<T> row) => AddRow(row);

    /// <summary>Adds a row only the objects of the kind that are <typeparamref name="TSub"/> have.</summary>
    public void Add<TSub>(Row<TSub> row)
        where TSub : class, T => AddRow(row);

    /// <summary>Adds a group of rows every object of the kind has.</summary>
    public void Add(IEnumerable<Row<T>> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        foreach (Row<T> row in rows)
        {
            AddRow(row);
        }
    }

    /// <summary>Adds a group of rows only the objects of the kind that are <typeparamref name="TSub"/> have.</summary>
    public void Add<TSub>(IEnumerable<Row<TSub>> rows)
        where TSub : class, T
    {
        ArgumentNullException.ThrowIfNull(rows);
        foreach (Row<TSub> row in rows)
        {
            AddRow(row);
        }
    }

    /// <inheritdoc />
    public IEnumerator<TableRow> GetEnumerator() => Rows.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>How one part of a key is written in an address.</summary>
public enum KeyPart
{
    /// <summary>A whole number from 0, such as a position.</summary>
    Index,

    /// <summary>An unsigned engine id.</summary>
    Id,

    /// <summary>A GUID in the D form.</summary>
    Guid,

    /// <summary>A name: letters, digits, dots, hyphens and underscores.</summary>
    Name,

    /// <summary>An unsigned engine number that is no position, such as a placeholder's index.</summary>
    Number,
}

/// <summary>
/// The key of an object below its parent, as the address writes it after the segment: one or
/// more parts, such as <c>256</c> in <c>slide/256</c> or <c>1/2</c> in <c>cell/1/2</c>.
/// </summary>
public sealed class Key : IEquatable<Key>
{
    private Key(IReadOnlyList<string> parts) => Parts = parts;

    /// <summary>The key of an object that is the only one of its kind below its parent, such as a slide's notes.</summary>
    public static Key None { get; } = new([]);

    /// <summary>The parts as the address writes them.</summary>
    public IReadOnlyList<string> Parts { get; }

    /// <summary>A key of whole numbers, such as positions.</summary>
    public static Key Of(params int[] parts) => new([.. parts.Select(static part => part.ToString(CultureInfo.InvariantCulture))]);

    /// <summary>A key of one engine id.</summary>
    public static Key Of(uint id) => new([id.ToString(CultureInfo.InvariantCulture)]);

    /// <summary>A key of one GUID.</summary>
    public static Key Of(Guid id) => new([id.ToString("D")]);

    /// <summary>A key of one name, with an optional position after it.</summary>
    public static Key Of(string name, int? index = null) =>
        new(index is null ? [name] : [name, index.Value.ToString(CultureInfo.InvariantCulture)]);

    /// <summary>A key of one name and an unsigned engine number after it.</summary>
    public static Key Of(string name, uint number) => new([name, number.ToString(CultureInfo.InvariantCulture)]);

    internal static Key Parsed(IReadOnlyList<string> parts) => new(parts);

    /// <summary>The part at <paramref name="position"/> as a whole number.</summary>
    public int Index(int position = 0) => int.Parse(Parts[position], NumberStyles.None, CultureInfo.InvariantCulture);

    /// <summary>The part at <paramref name="position"/> as an engine id.</summary>
    public uint Id(int position = 0) => uint.Parse(Parts[position], NumberStyles.None, CultureInfo.InvariantCulture);

    /// <summary>The part at <paramref name="position"/> as an unsigned engine number.</summary>
    public uint Number(int position = 0) => uint.Parse(Parts[position], NumberStyles.None, CultureInfo.InvariantCulture);

    /// <summary>The part at <paramref name="position"/> as a GUID.</summary>
    public Guid Guid(int position = 0) => System.Guid.ParseExact(Parts[position], "D");

    /// <summary>The part at <paramref name="position"/> as a name.</summary>
    public string Name(int position = 0) => Parts[position];

    /// <summary>Whether a part is present at <paramref name="position"/>.</summary>
    public bool Has(int position) => position < Parts.Count;

    /// <inheritdoc />
    public bool Equals(Key? other) => other is not null && Parts.SequenceEqual(other.Parts, StringComparer.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as Key);

    /// <inheritdoc />
    public override int GetHashCode() => string.Join('/', Parts).GetHashCode(StringComparison.Ordinal);

    /// <inheritdoc />
    public override string ToString() => string.Join('/', Parts);
}

/// <summary>
/// Where objects of <typeparamref name="T"/> sit below a <typeparamref name="TParent"/>: the
/// address segment, the key, and the engine calls that find, list, delete and move them. A null
/// <see cref="Delete"/> or <see cref="Move"/> means the kind cannot be deleted or moved there.
/// </summary>
public sealed class Locator<TParent, T>
    where TParent : class
    where T : class
{
    /// <summary>The address segment, such as <c>shape</c>.</summary>
    public required string Segment { get; init; }

    /// <summary>The parts of the key after the segment, empty for an object that is the only one of its kind.</summary>
    public IReadOnlyList<KeyPart> Key { get; init; } = [];

    /// <summary>The object with a key, or null when there is none.</summary>
    public required Func<TParent, Key, T?> Find { get; init; }

    /// <summary>Every object below the parent, in document order.</summary>
    public required Func<TParent, IEnumerable<T>> List { get; init; }

    /// <summary>The key of an object below its parent.</summary>
    public required Func<TParent, T, Key> KeyOf { get; init; }

    /// <summary>Deletes an object, or null when the kind cannot be deleted here.</summary>
    public Action<TParent, T>? Delete { get; init; }

    /// <summary>Moves an object to a position, or null when the kind cannot be moved here.</summary>
    public Action<TParent, T, int>? Move { get; init; }
}

/// <summary>
/// A second way to address objects a <see cref="Locator{TParent, T}"/> finds, such as a
/// placeholder by its type. It finds every match; none is not found and more than one is ambiguous.
/// The canonical address of a match is the locator's.
/// </summary>
public sealed class AliasLocator<TParent, T>
    where TParent : class
    where T : class
{
    /// <summary>The address segment, such as <c>placeholder</c>.</summary>
    public required string Segment { get; init; }

    /// <summary>The required parts of the key.</summary>
    public required IReadOnlyList<KeyPart> Key { get; init; }

    /// <summary>A part that may follow the required ones.</summary>
    public KeyPart? Optional { get; init; }

    /// <summary>Every object the key matches.</summary>
    public required Func<TParent, Key, IReadOnlyList<T>> Find { get; init; }

    /// <summary>The keys the parent's objects can be addressed by, in document order.</summary>
    public required Func<TParent, IEnumerable<Key>> List { get; init; }

    /// <summary>What the alias matches, written into the schema.</summary>
    public required string Description { get; init; }
}
