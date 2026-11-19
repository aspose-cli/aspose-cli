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
    public static TableReadOutcome Run<TDocument>(OutputPipeline<TDocument> outputs, ITableDocuments<TDocument> documents, TableReadRequest request)
        where TDocument : class
    {
        ArgumentNullException.ThrowIfNull(outputs);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(request);
        return TableErrors.Guard(documents.Table, "Reading the document", () => RunCore(outputs, documents, request));
    }

    private static TableReadOutcome RunCore<TDocument>(OutputPipeline<TDocument> outputs, ITableDocuments<TDocument> documents, TableReadRequest request)
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
        var kinds = new HashSet<string>(request.Kinds, StringComparer.Ordinal);
        (object Item, ObjectKind Kind, string Address)[] selected =
            [.. Walk(table, root.Item, root.Kind, root.Address).Where(found => kinds.Count == 0 || kinds.Contains(found.Kind.Name))];
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
            objects.Add(new TableObject { At = address, Kind = kind.Name, Props = Read(table, kind, item, address) });
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
            },
            Warnings = opened.Warnings.Count == 0 ? null : opened.Warnings,
            License = EnvelopeParts.License(state),
        };
    }

    /// <summary>Searches the text rows the table names.</summary>
    public static TableSearchOutcome Search<TDocument>(OutputPipeline<TDocument> outputs, ITableDocuments<TDocument> documents, TableSearchRequest request)
        where TDocument : class
    {
        ArgumentNullException.ThrowIfNull(outputs);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(request);
        return TableErrors.Guard(documents.Table, "Searching the document", () => SearchCore(outputs, documents, request));
    }

    private static TableSearchOutcome SearchCore<TDocument>(OutputPipeline<TDocument> outputs, ITableDocuments<TDocument> documents, TableSearchRequest request)
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
                if (target is null || ReadRow(table, target, item, address)?.GetValue<string>() is not { } text)
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

    /// <summary>Every object of a subtree in document order: the object, then each placement's objects below it.</summary>
    internal static IEnumerable<(object Item, ObjectKind Kind, string Address)> Walk(DocumentTable table, object item, ObjectKind kind, string address)
    {
        yield return (item, kind, address);
        foreach (Placement placement in table.Placements.Where(candidate => candidate.Parent == kind))
        {
            foreach (object child in AddressResolver.Engine(table, address, () => placement.List(item).ToArray()))
            {
                string below = AddressResolver.Below(table, address, placement, item, child);
                foreach ((object, ObjectKind, string) found in Walk(table, child, placement.Kind, below))
                {
                    yield return found;
                }
            }
        }
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
    private static JsonObject Read(DocumentTable table, ObjectKind kind, object item, string address)
    {
        try
        {
            return kind.Read(item);
        }
        catch (RowReadException failure)
        {
            throw TableErrors.RowRead(table, null, address, failure);
        }
    }

    private static JsonNode? ReadRow(DocumentTable table, TableRow row, object item, string address)
    {
        try
        {
            return row.Read(item);
        }
        catch (Exception exception) when (exception is not CliException and not OperationCanceledException && TableErrors.FromEngine(exception, table.Engine))
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
