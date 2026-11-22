using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Sdk.Tables;

/// <summary>
/// A parameter of the one engine call an add or an action makes, named after the engine's
/// parameter; the caller states every required one, and one not given is not passed.
/// </summary>
public sealed class Argument
{
    private Argument(string name, ValueKind kind, string description, bool required, IReadOnlyList<ObjectKind>? addresses = null, bool resolvedByTheCall = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Name = name;
        Kind = kind;
        Description = description;
        Required = required;
        Addresses = addresses;
        ResolvedByTheCall = resolvedByTheCall;
    }

    /// <summary>The kinds of object the argument's address may name, or null for an argument that is no address.</summary>
    public IReadOnlyList<ObjectKind>? Addresses { get; }

    /// <summary>
    /// Whether the call itself finds the object, in another document it opens, through
    /// <see cref="TableActionContext{TDocument}.Find{T}"/>; otherwise the edit finds it in the
    /// edited document before the call.
    /// </summary>
    public bool ResolvedByTheCall { get; }

    /// <summary>
    /// A parameter whose value is the address of an object of <paramref name="kind"/>, such as the
    /// worksheet a call is made on. The address is checked against the table with the batch and
    /// the object found in the edited document before the call: <see cref="Arguments.Get{T}"/>
    /// gives the engine object, <see cref="Arguments.AddressOf"/> its address.
    /// </summary>
    public static Argument Address(string name, ObjectKind kind, string description, bool required = true) =>
        new(name, ValueKinds.AddressText, description, required, [kind ?? throw new ArgumentNullException(nameof(kind))]);

    /// <summary>
    /// A parameter whose value is the address of an object of any of <paramref name="kinds"/>, such
    /// as the sibling a new object is inserted before, which may be a paragraph or a table; it is
    /// found in the edited document before the call, as a one-kind address is.
    /// </summary>
    public static Argument Address(string name, IReadOnlyList<ObjectKind> kinds, string description, bool required = true)
    {
        ArgumentNullException.ThrowIfNull(kinds);
        if (kinds.Count == 0 || kinds.Any(static kind => kind is null))
        {
            throw new ArgumentException("An address argument names at least one kind, and no null one.", nameof(kinds));
        }

        return new(name, ValueKinds.AddressText, description, required, kinds);
    }

    /// <summary>
    /// A parameter whose value is the address of an object of <paramref name="kind"/> that the call
    /// finds itself, through <see cref="TableActionContext{TDocument}.Find{T}"/>, such as a range of
    /// another workbook it opens.
    /// </summary>
    public static Argument AddressFoundByTheCall(string name, ObjectKind kind, string description, bool required = true) =>
        new(name, ValueKinds.AddressText, description, required, [kind ?? throw new ArgumentNullException(nameof(kind))], resolvedByTheCall: true);

    /// <summary>The parameter's name.</summary>
    public string Name { get; }

    /// <summary>How its value is written.</summary>
    public ValueKind Kind { get; }

    /// <summary>What it is, written into the schema.</summary>
    public string Description { get; }

    /// <summary>Whether the caller must give it.</summary>
    public bool Required { get; }

    /// <summary>A parameter the caller must give.</summary>
    public static Argument Of<T>(string name, ValueKind<T> kind, string description) => new(name, kind, description, required: true);

    /// <summary>A parameter the caller may leave out.</summary>
    public static Argument Optional<T>(string name, ValueKind<T> kind, string description) => new(name, kind, description, required: false);
}

/// <summary>The values the caller gave for the parameters of one engine call.</summary>
public sealed class Arguments
{
    private readonly IReadOnlyDictionary<string, object?> _values;

    internal Arguments(IReadOnlyDictionary<string, object?> values) => _values = values;

    internal static Arguments None { get; } = new(new Dictionary<string, object?>());

    /// <summary>Whether the caller gave <paramref name="name"/>.</summary>
    public bool Has(string name) => _values.ContainsKey(name);

    /// <summary>The value of a parameter the caller gave; for an address, the engine object the edit found at it.</summary>
    public T Get<T>(string name) =>
        _values.TryGetValue(name, out object? value)
            ? value switch
            {
                FoundAddress found => (T)found.Located.Item,
                AddressValue => throw new InvalidOperationException($"Argument '{name}' is an address the call finds itself; use TableActionContext.Find."),
                _ => (T)value!,
            }
            : throw new InvalidOperationException($"Argument '{name}' was not given; declare it required or test Has first.");

    /// <summary>The address an address parameter named, in its canonical form once the edit found it.</summary>
    public string AddressOf(string name) =>
        _values.TryGetValue(name, out object? value)
            ? value switch
            {
                FoundAddress found => found.Located.Address,
                AddressValue address => address.Address.Text,
                _ => throw new InvalidOperationException($"Argument '{name}' is no address."),
            }
            : throw new InvalidOperationException($"Argument '{name}' was not given; declare it required or test Has first.");

    /// <summary>These arguments with each address the edit finds looked up in <paramref name="document"/>.</summary>
    internal Arguments Resolve(DocumentTable table, object document)
    {
        if (!_values.Values.Any(static value => value is AddressValue { ResolvedByTheCall: false }))
        {
            return this;
        }

        var resolved = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach ((string name, object? value) in _values)
        {
            resolved[name] = value is AddressValue { ResolvedByTheCall: false } address
                ? new FoundAddress(AddressResolver.Resolve(table, document, address.Address))
                : value;
        }

        return new Arguments(resolved);
    }

    /// <summary>An address argument, checked against the table, not yet looked up.</summary>
    internal sealed record AddressValue(TableAddress Address, bool ResolvedByTheCall);

    /// <summary>An address argument the edit looked up.</summary>
    private sealed record FoundAddress(Located Located);

    /// <summary>The address value of a parameter, for a lookup the call makes itself.</summary>
    internal TableAddress AddressValueOf(string name) =>
        _values.TryGetValue(name, out object? value) && value is AddressValue address
            ? address.Address
            : throw new InvalidOperationException($"Argument '{name}' is no address the call finds itself.");
}

/// <summary>An add: one name for one engine call that creates an object of <typeparamref name="T"/> below a <typeparamref name="TParent"/>.</summary>
public sealed class Adder<TParent, T>
    where TParent : class
    where T : class
{
    /// <summary>The add's name, such as <c>autoShape</c>.</summary>
    public required string Name { get; init; }

    /// <summary>The engine call, written into the schema.</summary>
    public required string Description { get; init; }

    /// <summary>The engine call's parameters.</summary>
    public IReadOnlyList<Argument> Arguments { get; init; } = [];

    /// <summary>Makes the engine call.</summary>
    public required Func<TParent, Arguments, T> Create { get; init; }
}

/// <summary>
/// What an action returns: the engine's return value, written as JSON, and the address of the
/// object it returns, if it returns one, which the report reads back from the saved file.
/// </summary>
/// <param name="Returned">The engine's return value, or null when the call returns nothing.</param>
/// <param name="At">The address of the object the call returned, or null.</param>
public sealed record ActionResult(JsonNode? Returned, string? At = null);

/// <summary>An action: one name for one engine call on the whole document, whose parameters the caller states.</summary>
public sealed class TableAction<TDocument>
    where TDocument : class
{
    /// <summary>The action's name, such as <c>replace_text</c>.</summary>
    public required string Name { get; init; }

    /// <summary>The engine call, written into the schema.</summary>
    public required string Description { get; init; }

    /// <summary>The engine call's parameters.</summary>
    public IReadOnlyList<Argument> Arguments { get; init; } = [];

    /// <summary>Makes the engine call; the context opens another document the call takes content from.</summary>
    public required Func<TDocument, Arguments, TableActionContext<TDocument>, ActionResult> Run { get; init; }
}

/// <summary>One match the engine's own search call found in a text row.</summary>
/// <param name="Text">The matched text, as the engine gives it.</param>
/// <param name="Rect">The engine's rectangle of the match as the engine writes it, or null.</param>
public sealed record TableMatch(string Text, string? Rect);

/// <summary>One kind of content the inventory counts.</summary>
/// <param name="Key">The inventory key, such as <c>slides</c>.</param>
/// <param name="Description">What is counted.</param>
/// <param name="Kind">
/// The table's kind of the counted objects, or null for content the table does not address;
/// the inventory lists the count under read exactly when the table places this kind.
/// </param>
public sealed record InventoryKind(string Key, string Description, ObjectKind? Kind);

/// <summary>
/// A product's object table: the kinds of its documents, where each sits and how it is found,
/// added, deleted and moved, the actions, the text rows a search looks in and the inventory. The
/// SDK reads, edits, reports and writes the schema from it; a product declares it once, at compile time.
/// </summary>
/// <typeparam name="TDocument">The engine's document type.</typeparam>
public sealed class DocumentTable<TDocument> : DocumentTable
    where TDocument : class
{
    /// <summary>Starts a table whose root is <paramref name="root"/>, the document itself.</summary>
    public DocumentTable(ObjectKind<TDocument> root)
        : base(root)
    {
    }

    /// <summary>Declares where objects of <paramref name="kind"/> sit below <paramref name="parent"/>.</summary>
    public void Place<TParent, T>(ObjectKind<TParent> parent, ObjectKind<T> kind, Locator<TParent, T> locator)
        where TParent : class
        where T : class
    {
        ArgumentNullException.ThrowIfNull(locator);
        Action<TParent, T>? delete = locator.Delete;
        Action<TParent, T, int>? move = locator.Move;
        Func<TParent, Key, T?>? find = locator.Find;
        Func<TParent, Key, IReadOnlyList<T>>? findAll = locator.FindAll;
        if ((find is null) == (findAll is null))
        {
            throw new ArgumentException($"The locator of '{kind.Name}' gives Find or FindAll, and only one of them.", nameof(locator));
        }

        AddPlacement(new Placement(
            parent,
            kind,
            locator.Segment,
            locator.Key,
            findAll is null
                ? (owner, key) => find!((TParent)owner, key) is { } found ? [found] : []
                : (owner, key) => [.. findAll((TParent)owner, key)],
            owner => locator.List((TParent)owner),
            (owner, item) => locator.KeyOf((TParent)owner, (T)item),
            delete is null ? null : (owner, item) => delete((TParent)owner, (T)item),
            move is null ? null : (owner, item, to) => move((TParent)owner, (T)item, to))
        {
            ListedWhenNamed = locator.ListedWhenNamed,
        });
    }

    /// <summary>Declares a second way to address objects of <paramref name="kind"/> below <paramref name="parent"/>.</summary>
    public void Alias<TParent, T>(ObjectKind<TParent> parent, ObjectKind<T> kind, AliasLocator<TParent, T> alias)
        where TParent : class
        where T : class
    {
        ArgumentNullException.ThrowIfNull(alias);
        AddAlias(new AliasPlacement(
            PlacementOf(parent, kind),
            alias.Segment,
            alias.Key,
            alias.Optional,
            (owner, key) => [.. alias.Find((TParent)owner, key)],
            owner => alias.List((TParent)owner),
            alias.Description));
    }

    /// <summary>
    /// Declares an add that creates objects of <paramref name="kind"/> below <paramref name="parent"/>,
    /// on the parents that are <typeparamref name="TParent"/>: the engine type whose member the add
    /// calls, which may be a subtype of the parent kind's, as a row's can.
    /// </summary>
    public void Adds<TKind, TParent, T>(ObjectKind<TKind> parent, ObjectKind<T> kind, Adder<TParent, T> adder)
        where TKind : class
        where TParent : class, TKind
        where T : class
    {
        ArgumentNullException.ThrowIfNull(adder);
        AddAdder(new AdderEntry(
            adder.Name,
            adder.Description,
            PlacementOf(parent, kind),
            typeof(TParent),
            adder.Arguments,
            (owner, arguments) => adder.Create((TParent)owner, arguments)));
    }

    /// <summary>
    /// Declares that the objects of every kind placed below <paramref name="parent"/> are listed
    /// together, in the order <paramref name="position"/> gives each in its parent, such as a
    /// paragraph's and a table's position among a story's child nodes; without it, read lists one
    /// placement's objects after another's.
    /// </summary>
    public void Orders<TParent>(ObjectKind<TParent> parent, Func<TParent, object, int> position)
        where TParent : class
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(position);
        AddOrder(parent, (owner, child) => position((TParent)owner, child));
    }

    /// <summary>Declares an action.</summary>
    public void Action(TableAction<TDocument> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        AddAction(new ActionEntry(action.Name, action.Description, action.Arguments, (document, arguments, context) => action.Run((TDocument)document, arguments, (TableActionContext<TDocument>)context)));
    }

    /// <summary>
    /// Declares that a search looks in the text row <paramref name="row"/> of <paramref name="kind"/>
    /// through the engine's own search call, which gives each match's text and the engine's
    /// rectangle rather than an offset in the row's text.
    /// </summary>
    public void SearchesWith<T>(ObjectKind<T> kind, string row, Func<T, TextSearch, IEnumerable<TableMatch>> search)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(search);
        AddEngineSearch(kind, row, (item, query) => search((T)item, query));
    }

    /// <summary>Declares the inventory: the kinds of content it counts and how they are counted.</summary>
    public void Inventory(IReadOnlyList<InventoryKind> kinds, Func<TDocument, IReadOnlyDictionary<string, long>> count)
    {
        ArgumentNullException.ThrowIfNull(count);
        SetInventory(kinds, document => count((TDocument)document));
    }
}

/// <summary>The type-erased object table the SDK's reader, editor and schema writer walk.</summary>
public abstract class DocumentTable
{
    private readonly List<Placement> _placements = [];
    private readonly List<AliasPlacement> _aliases = [];
    private readonly List<AdderEntry> _adders = [];
    private readonly List<ActionEntry> _actions = [];
    private readonly List<(ObjectKind Kind, string Row)> _text = [];
    private readonly List<(ObjectKind Kind, string Row, Func<object, TextSearch, IEnumerable<TableMatch>> Search)> _engineSearches = [];
    private readonly Dictionary<ObjectKind, Func<object, object, int>> _orders = [];
    private IReadOnlyList<InventoryKind> _inventoryKinds = [];
    private Func<object, IReadOnlyDictionary<string, long>> _count = static _ => new Dictionary<string, long>();

    private protected DocumentTable(ObjectKind root)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
    }

    /// <summary>The document's own kind, addressed by its name alone.</summary>
    public ObjectKind Root { get; }

    /// <summary>The id, relative to the product, every table's operation document schema is published under.</summary>
    public const string SchemaId = "ops";

    internal IReadOnlyList<Placement> Placements => _placements;

    internal IReadOnlyList<AliasPlacement> Aliases => _aliases;

    internal IReadOnlyList<AdderEntry> Adders => _adders;

    internal IReadOnlyList<ActionEntry> Actions => _actions;

    internal IReadOnlyList<InventoryKind> InventoryKinds => _inventoryKinds;

    internal IReadOnlyList<(ObjectKind Kind, string Row)> TextRows => _text;

    /// <summary>The text rows a search looks in through the engine's own search call.</summary>
    internal IReadOnlyList<(ObjectKind Kind, string Row, Func<object, TextSearch, IEnumerable<TableMatch>> Search)> EngineSearches => _engineSearches;

    /// <summary>The position of a child among its parent's children, for a parent kind whose children are listed together; null for any other.</summary>
    internal Func<object, object, int>? OrderOf(ObjectKind parent) => _orders.GetValueOrDefault(parent);

    /// <summary>Every kind, the root's first, in the order they were placed.</summary>
    internal IReadOnlyList<ObjectKind> Kinds => [Root, .. _placements.Select(static placement => placement.Kind).Distinct()];

    /// <summary>Declares a text row a search looks in.</summary>
    public void SearchesText(ObjectKind kind, string row)
    {
        CheckTextRow(kind, row);
        _text.Add((kind, row));
    }

    private protected void AddEngineSearch(ObjectKind kind, string row, Func<object, TextSearch, IEnumerable<TableMatch>> search)
    {
        CheckTextRow(kind, row);
        _engineSearches.Add((kind, row, search));
    }

    private static void CheckTextRow(ObjectKind kind, string row)
    {
        ArgumentNullException.ThrowIfNull(kind);
        if (!kind.Rows.Any(candidate => candidate.Name == row && candidate.Kind == ValueKinds.Text))
        {
            throw new ArgumentException($"Kind '{kind.Name}' has no text row '{row}'.", nameof(row));
        }
    }

    /// <summary>Counts the content of a document, by inventory key.</summary>
    internal IReadOnlyDictionary<string, long> Count(object document) => _count(document);

    private protected void SetInventory(IReadOnlyList<InventoryKind> kinds, Func<object, IReadOnlyDictionary<string, long>> count)
    {
        ArgumentNullException.ThrowIfNull(kinds);
        _inventoryKinds = kinds;
        _count = count;
    }

    private protected void AddPlacement(Placement placement)
    {
        if (_placements.Any(existing => existing.Parent == placement.Parent && existing.Segment == placement.Segment)
            || _aliases.Any(existing => existing.Target.Parent == placement.Parent && existing.Segment == placement.Segment))
        {
            throw new ArgumentException($"Kind '{placement.Parent.Name}' has segment '{placement.Segment}' twice.", nameof(placement));
        }

        _placements.Add(placement);
    }

    private protected void AddAlias(AliasPlacement alias)
    {
        if (_placements.Any(existing => existing.Parent == alias.Target.Parent && existing.Segment == alias.Segment))
        {
            throw new ArgumentException($"Kind '{alias.Target.Parent.Name}' has segment '{alias.Segment}' twice.", nameof(alias));
        }

        _aliases.Add(alias);
    }

    private protected void AddOrder(ObjectKind parent, Func<object, object, int> position)
    {
        if (!_orders.TryAdd(parent, position))
        {
            throw new ArgumentException($"Kind '{parent.Name}' declares the order of its children twice.", nameof(parent));
        }
    }

    private protected void AddAdder(AdderEntry adder)
    {
        if (_adders.Any(existing => existing.Name == adder.Name && existing.Placement.Parent == adder.Placement.Parent))
        {
            throw new ArgumentException($"Add '{adder.Name}' is declared twice below '{adder.Placement.Parent.Name}'.", nameof(adder));
        }

        _adders.Add(adder);
    }

    private protected void AddAction(ActionEntry action)
    {
        if (_actions.Any(existing => existing.Name == action.Name))
        {
            throw new ArgumentException($"Action '{action.Name}' is declared twice.", nameof(action));
        }

        _actions.Add(action);
    }

    private protected Placement PlacementOf(ObjectKind parent, ObjectKind kind) =>
        _placements.FirstOrDefault(placement => placement.Parent == parent && placement.Kind == kind)
            ?? throw new ArgumentException($"Kind '{kind.Name}' is not placed below '{parent.Name}'; place it first.", nameof(kind));
}

/// <summary>Where a kind sits below a parent kind, type-erased; <see cref="Find"/> gives every object a key matches.</summary>
internal sealed record Placement(
    ObjectKind Parent,
    ObjectKind Kind,
    string Segment,
    IReadOnlyList<KeyPart> Key,
    Func<object, Key, IReadOnlyList<object>> Find,
    Func<object, IEnumerable<object>> List,
    Func<object, object, Key> KeyOf,
    Action<object, object>? Delete,
    Action<object, object, int>? Move)
{
    /// <summary>Whether read lists the objects only when --kind names their kind.</summary>
    public bool ListedWhenNamed { get; init; }
}

/// <summary>A second way to address the objects of a placement, type-erased.</summary>
internal sealed record AliasPlacement(
    Placement Target,
    string Segment,
    IReadOnlyList<KeyPart> Key,
    KeyPart? Optional,
    Func<object, Key, IReadOnlyList<object>> Find,
    Func<object, IEnumerable<Key>> List,
    string Description);

/// <summary>An add, type-erased.</summary>
internal sealed record AdderEntry(
    string Name,
    string Description,
    Placement Placement,
    Type Target,
    IReadOnlyList<Argument> Arguments,
    Func<object, Arguments, object> Create);

/// <summary>An action, type-erased.</summary>
internal sealed record ActionEntry(
    string Name,
    string Description,
    IReadOnlyList<Argument> Arguments,
    Func<object, Arguments, object, ActionResult> Run);
