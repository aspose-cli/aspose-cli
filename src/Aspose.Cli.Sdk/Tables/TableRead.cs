using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Sdk.Tables;

/// <summary>The fields every object table read takes; a product's request derives from it.</summary>
public abstract record TableReadRequest
{
    /// <summary>The document to read.</summary>
    public required string Input { get; init; }

    /// <summary>The password that opens the input.</summary>
    public Secret? Password { get; init; }

    /// <summary>The address of the object whose subtree is read (<c>--at</c>); null for the whole document.</summary>
    public string? At { get; init; }

    /// <summary>The address of the object the window starts at (<c>--from</c>); null for the first.</summary>
    public string? From { get; init; }

    /// <summary>The kinds listed (<c>--kind</c>); empty for every kind.</summary>
    public IReadOnlyList<string> Kinds { get; init; } = [];

    /// <summary>The most objects one window lists (<c>--max-objects</c>).</summary>
    public int MaxObjects { get; init; } = TableRead.DefaultMaxObjects;
}

/// <summary>What an object table read reports; a product's result states each field.</summary>
public sealed record TableReadOutcome
{
    /// <summary>The read document.</summary>
    public required InputInfo Input { get; init; }

    /// <summary>The objects of the window, in document order.</summary>
    public required IReadOnlyList<TableObject> Objects { get; init; }

    /// <summary>The whole document's inventory.</summary>
    public required TableInventory Inventory { get; init; }

    /// <summary>The engine's warnings, or null when it reported none.</summary>
    public IReadOnlyList<EngineWarning>? EngineWarnings { get; init; }

    /// <summary>The window.</summary>
    public required ResultWindow Window { get; init; }

    /// <summary>The CLI's own warnings about the document, such as left-out external resources, or null.</summary>
    public IReadOnlyList<Warning>? Warnings { get; init; }

    /// <summary>The license mode when the engine ran without a license.</summary>
    public LicenseInfo? License { get; init; }
}

/// <summary>The fields every object table search takes; a product's request derives from it.</summary>
public abstract record TableSearchRequest
{
    /// <summary>The document to search.</summary>
    public required string Input { get; init; }

    /// <summary>The password that opens the input.</summary>
    public Secret? Password { get; init; }

    /// <summary>The search.</summary>
    public required SearchQuery Query { get; init; }
}

/// <summary>What an object table search reports; a product's result states each field.</summary>
public sealed record TableSearchOutcome
{
    /// <summary>The searched document.</summary>
    public required InputInfo Input { get; init; }

    /// <summary>The hits of the window, in document order.</summary>
    public required IReadOnlyList<TableHit> Hits { get; init; }

    /// <summary>The engine's warnings, or null when it reported none.</summary>
    public IReadOnlyList<EngineWarning>? EngineWarnings { get; init; }

    /// <summary>The window.</summary>
    public required ResultWindow Window { get; init; }

    /// <summary>The CLI's own warnings about the document, such as left-out external resources, or null.</summary>
    public IReadOnlyList<Warning>? Warnings { get; init; }

    /// <summary>The license mode when the engine ran without a license.</summary>
    public LicenseInfo? License { get; init; }
}

/// <summary>
/// The object table's read and search: every object of a subtree in document order, each with
/// all its rows; a window of at most <c>--max-objects</c>, whose <c>next</c> names the object the
/// next window starts at; and the whole document's inventory. A search matches the text rows the
/// table names and reports each match by the object's address.
/// </summary>
public static class TableRead
{
    /// <summary>The objects a window lists when <c>--max-objects</c> is not given.</summary>
    public const int DefaultMaxObjects = 200;

    /// <summary>The most objects a window can list.</summary>
    public const int MaximumObjects = 10_000;

    /// <summary>The option that names where the next window starts.</summary>
    public const string FromOption = "--from";

    /// <summary>Reads a window of objects.</summary>
    public static TableReadOutcome Run<TDocument>(OutputPipeline outputs, ITableDocuments<TDocument> documents, TableReadRequest request)
        where TDocument : class
    {
        ArgumentNullException.ThrowIfNull(outputs);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(request);
        return TableErrors.Guard("Reading the document", () => RunCore(outputs, documents, request));
    }

    private static TableReadOutcome RunCore<TDocument>(OutputPipeline outputs, ITableDocuments<TDocument> documents, TableReadRequest request)
        where TDocument : class
    {
        // The license is applied before the engine opens anything.
        LicenseState state = outputs.License;
        DocumentTable table = documents.Table;
        var warnings = new EngineWarningLog();
        using IOpenedDocument<TDocument> opened = documents.Open(request.Input, request.Password, warnings);
        object document = opened.Document;
        Located root = request.At is null
            ? new Located(document, null, null, table.Root, table.Root.Name, null)
            : AddressResolver.Resolve(table, document, Parse(table, request.At, "--at"));
        string? from = request.From is null
            ? null
            : AddressResolver.Resolve(table, document, Parse(table, request.From, FromOption)).Address;
        ((object Item, ObjectKind Kind, string Address)[] selected, IReadOnlyList<string> unnamed) = Select(table, root, request.Kinds);
        int start = 0;
        if (from is not null)
        {
            start = Array.FindIndex(selected, found => found.Address == from);
            if (start < 0)
            {
                throw CliErrors.OptionInvalid(FromOption, $"{from} is not among the objects this read selects");
            }
        }

        var objects = new List<TableObject>();
        for (int position = start; position < selected.Length && objects.Count < request.MaxObjects; position++)
        {
            (object item, ObjectKind kind, string address) = selected[position];
            objects.Add(new TableObject { At = address, Kind = kind.Name, Props = Read(kind, item, address) });
        }

        int next = start + objects.Count;
        return new TableReadOutcome
        {
            Input = FileObjects.Input(request.Input, opened.FormatId, opened.Protection.Encrypted),
            Objects = objects,
            Inventory = Inventory(table, document),
            EngineWarnings = warnings.Warnings.Count == 0 ? null : warnings.Warnings,
            Window = new ResultWindow
            {
                Unit = "object",
                Returned = objects.Count,
                Total = selected.Length - start,
                Truncated = next < selected.Length,
                Next = next < selected.Length ? ResultWindow.Options((FromOption, selected[next].Address)) : null,
                NotListedUnlessNamed = unnamed.Count == 0 ? null : unnamed,
            },
            Warnings = opened.Warnings.Count == 0 ? null : opened.Warnings,
            License = EnvelopeParts.License(state),
        };
    }

    /// <summary>Searches the text rows the table names.</summary>
    public static TableSearchOutcome Search<TDocument>(OutputPipeline outputs, ITableDocuments<TDocument> documents, TableSearchRequest request)
        where TDocument : class
    {
        ArgumentNullException.ThrowIfNull(outputs);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(request);
        return TableErrors.Guard("Searching the document", () => SearchCore(outputs, documents, request));
    }

    private static TableSearchOutcome SearchCore<TDocument>(OutputPipeline outputs, ITableDocuments<TDocument> documents, TableSearchRequest request)
        where TDocument : class
    {
        LicenseState state = outputs.License;
        DocumentTable table = documents.Table;
        var warnings = new EngineWarningLog();
        using IOpenedDocument<TDocument> opened = documents.Open(request.Input, request.Password, warnings);
        SearchHits<TableHit> hits = request.Query.Collect<TableHit>();
        foreach ((object item, ObjectKind kind, string address) in Walk(table, opened.Document, table.Root, table.Root.Name))
        {
            foreach ((ObjectKind _, string row) in table.TextRows.Where(text => text.Kind == kind))
            {
                TableRow? target = kind.RowsOf(item).FirstOrDefault(candidate => candidate.Name == row);
                if (target is null || ReadRow(target, item, address)?.GetValue<string>() is not { } text)
                {
                    continue;
                }

                foreach ((int index, int length) in request.Query.Text.Find(text))
                {
                    if (!hits.Offer(() => new TableHit { At = address, Row = row, Offset = index, Length = length, Text = text.Substring(index, length) }))
                    {
                        goto Done;
                    }
                }
            }

            foreach ((ObjectKind _, string row, Func<object, TextSearch, IEnumerable<TableMatch>> search) in table.EngineSearches.Where(engine => engine.Kind == kind))
            {
                foreach (TableMatch match in AddressResolver.Engine(address, () => search(item, request.Query.Text).ToArray()))
                {
                    if (!hits.Offer(() => new TableHit { At = address, Row = row, Text = match.Text, Rect = match.Rect }))
                    {
                        goto Done;
                    }
                }
            }
        }

    Done:
        return new TableSearchOutcome
        {
            Input = FileObjects.Input(request.Input, opened.FormatId, opened.Protection.Encrypted),
            Hits = hits.Hits,
            EngineWarnings = warnings.Warnings.Count == 0 ? null : warnings.Warnings,
            Window = hits.Window(),
            Warnings = opened.Warnings.Count == 0 ? null : opened.Warnings,
            License = EnvelopeParts.License(state),
        };
    }

    /// <summary>
    /// Every object of a subtree in document order: the object, then each placement's objects below
    /// it, through the placements <paramref name="descend"/> admits (all when it is null).
    /// </summary>
    internal static IEnumerable<(object Item, ObjectKind Kind, string Address)> Walk(
        DocumentTable table, object item, ObjectKind kind, string address, Func<Placement, bool>? descend = null)
    {
        yield return (item, kind, address);
        foreach ((Placement placement, object child) in Children(table, item, kind, address, descend))
        {
            string below = AddressResolver.Below(table, address, placement, item, child);
            foreach ((object, ObjectKind, string) found in Walk(table, child, placement.Kind, below, descend))
            {
                yield return found;
            }
        }
    }

    /// <summary>
    /// The children of an object: one placement's after another's, or all together in the order the
    /// table declares for the object's kind.
    /// </summary>
    private static IEnumerable<(Placement Placement, object Child)> Children(
        DocumentTable table, object item, ObjectKind kind, string address, Func<Placement, bool>? descend)
    {
        (Placement Placement, object Child)[] children = [.. table.Placements
            .Where(candidate => candidate.Parent == kind && (descend is null || descend(candidate)))
            .SelectMany(placement => AddressResolver.Engine(address, () => placement.List(item).ToArray()).Select(child => (placement, child)))];
        return table.OrderOf(kind) is { } position
            ? AddressResolver.Engine(address, () => children.OrderBy(found => position(item, found.Child)).ToArray())
            : children;
    }

    /// <summary>
    /// The objects a read lists below and from <paramref name="root"/>: without --kind every kind
    /// but those listed only when named, which it returns as not listed; with --kind the named
    /// kinds, walking only the placements that lead to one.
    /// </summary>
    internal static ((object Item, ObjectKind Kind, string Address)[] Selected, IReadOnlyList<string> NotListed) Select(
        DocumentTable table, Located root, IReadOnlyCollection<string> named)
    {
        var kinds = new HashSet<string>(named, StringComparer.Ordinal);
        HashSet<ObjectKind> reaching = [.. table.Placements.Select(static placement => placement.Kind).Distinct().Where(kind => Reaches(table, kind, kinds))];
        Func<Placement, bool> descend = kinds.Count == 0
            ? static placement => !placement.ListedWhenNamed
            : placement => reaching.Contains(placement.Kind);
        (object Item, ObjectKind Kind, string Address)[] selected =
            [.. Walk(table, root.Item, root.Kind, root.Address, descend).Where(found => kinds.Count == 0 || kinds.Contains(found.Kind.Name))];
        return (selected, kinds.Count == 0 ? ListedWhenNamedBelow(table, root.Kind) : []);
    }

    /// <summary>
    /// Whether <paramref name="kind"/> or a kind placed anywhere below it is one of
    /// <paramref name="named"/>: each kind is visited once, so a kind that holds itself, directly
    /// or through others, ends the search.
    /// </summary>
    private static bool Reaches(DocumentTable table, ObjectKind kind, IReadOnlySet<string> named)
    {
        var seen = new HashSet<ObjectKind> { kind };
        var pending = new Queue<ObjectKind>([kind]);
        while (pending.TryDequeue(out ObjectKind? current))
        {
            if (named.Contains(current.Name))
            {
                return true;
            }

            foreach (Placement placement in table.Placements.Where(candidate => candidate.Parent == current))
            {
                if (seen.Add(placement.Kind))
                {
                    pending.Enqueue(placement.Kind);
                }
            }
        }

        return false;
    }

    /// <summary>The kinds listed only when named that sit anywhere below <paramref name="kind"/>, in the table's order.</summary>
    private static IReadOnlyList<string> ListedWhenNamedBelow(DocumentTable table, ObjectKind kind)
    {
        var below = new HashSet<ObjectKind>();
        var pending = new Queue<ObjectKind>([kind]);
        while (pending.TryDequeue(out ObjectKind? parent))
        {
            foreach (Placement placement in table.Placements.Where(candidate => candidate.Parent == parent))
            {
                if (below.Add(placement.Kind))
                {
                    pending.Enqueue(placement.Kind);
                }
            }
        }

        return [.. table.Placements.Where(placement => placement.ListedWhenNamed && below.Contains(placement.Kind))
            .Select(static placement => placement.Kind.Name).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    /// The whole document's inventory, by the table's keys in declaration order: a kind is read
    /// when its objects are of a kind the table places, which read lists.
    /// </summary>
    internal static TableInventory Inventory(DocumentTable table, object document)
    {
        IReadOnlyDictionary<string, long> counts = table.Count(document);
        IReadOnlyList<ObjectKind> listed = table.Kinds;
        var read = new Dictionary<string, long>(StringComparer.Ordinal);
        var notRead = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (InventoryKind kind in table.InventoryKinds)
        {
            (kind.Kind is not null && listed.Contains(kind.Kind) ? read : notRead)[kind.Key] = counts.GetValueOrDefault(kind.Key);
        }

        return new TableInventory { Read = read, NotRead = notRead };
    }
    private static JsonObject Read(ObjectKind kind, object item, string address)
    {
        try
        {
            return kind.Read(item);
        }
        catch (RowReadException failure)
        {
            throw TableErrors.RowRead(null, address, failure);
        }
    }

    private static JsonNode? ReadRow(TableRow row, object item, string address)
    {
        try
        {
            return row.Read(item);
        }
        catch (Exception exception) when (ExceptionOrigin.FromEngine(exception))
        {
            throw TableErrors.Engine(null, address, row.Name, exception);
        }
    }

    private static TableAddress Parse(DocumentTable table, string text, string option)
    {
        try
        {
            return TableAddress.Parse(table, text);
        }
        catch (TableValueException invalid)
        {
            throw CliErrors.OptionInvalid(option, invalid.Message);
        }
    }

    /// <summary>Checks an address an option gives, before any document is opened.</summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> naming the option.</exception>
    public static void CheckAddress(DocumentTable table, string text, string option) => _ = Parse(table, text, option);
}
