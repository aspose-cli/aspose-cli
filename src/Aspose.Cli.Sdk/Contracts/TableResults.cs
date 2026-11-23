using System.Text.Json.Serialization;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>What an object table read found; <see cref="TableReadResult"/> states each field.</summary>
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

/// <summary>
/// Base of the result of a product's <c>read</c> on the object table. The fields are stated here
/// once for every product; a product's record names its schema and passes what the read found.
/// </summary>
public abstract record TableReadResult : WindowedResultEnvelope
{
    /// <summary>States what a read found, under the product's schema <paramref name="schema"/>.</summary>
    /// <param name="schema">The schema id relative to the product, such as <c>read-result</c>.</param>
    /// <param name="found">What the read found.</param>
    /// <exception cref="ArgumentException"><paramref name="schema"/> is not a relative schema id.</exception>
    protected TableReadResult(string schema, TableReadOutcome found)
        : base(schema, (found ?? throw new ArgumentNullException(nameof(found))).Window)
    {
        Input = found.Input;
        Objects = found.Objects;
        Inventory = found.Inventory;
        EngineWarnings = found.EngineWarnings;
        License = found.License;
        Warnings = found.Warnings;
    }

    /// <summary>The document read.</summary>
    [JsonPropertyOrder(-49)]
    public InputInfo Input { get; }

    /// <summary>The objects of the window, in document order, each with its address, kind and rows; last, since it grows with the document.</summary>
    [JsonPropertyOrder(1000)]
    public IReadOnlyList<TableObject> Objects { get; }

    /// <summary>How many objects of each kind of content the document holds: under read the kinds read lists, under notRead the others.</summary>
    [JsonPropertyOrder(850)]
    public TableInventory Inventory { get; }

    /// <summary>The warnings the engine reported while it loaded the document, as it reported them; omitted when there were none.</summary>
    [JsonPropertyOrder(860)]
    public IReadOnlyList<EngineWarning>? EngineWarnings { get; }
}

/// <summary>What an object table search found; <see cref="TableSearchResult"/> states each field.</summary>
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
/// Base of the result of a product's <c>search</c> on the object table. The fields are stated
/// here once for every product; a product's record names its schema and passes what the search found.
/// </summary>
public abstract record TableSearchResult : WindowedResultEnvelope
{
    /// <summary>States what a search found, under the product's schema <paramref name="schema"/>.</summary>
    /// <param name="schema">The schema id relative to the product, such as <c>search-result</c>.</param>
    /// <param name="found">What the search found.</param>
    /// <exception cref="ArgumentException"><paramref name="schema"/> is not a relative schema id.</exception>
    protected TableSearchResult(string schema, TableSearchOutcome found)
        : base(schema, (found ?? throw new ArgumentNullException(nameof(found))).Window)
    {
        Input = found.Input;
        Hits = found.Hits;
        EngineWarnings = found.EngineWarnings;
        License = found.License;
        Warnings = found.Warnings;
    }

    /// <summary>The searched document.</summary>
    [JsonPropertyOrder(-50)]
    public InputInfo Input { get; }

    /// <summary>The hits in document order: the address of the object, the row, the matched text and where the match is, as an offset and length in the row's text or as the rectangle the engine's own search gives; last, since it grows with the document.</summary>
    [JsonPropertyOrder(1000)]
    public IReadOnlyList<TableHit> Hits { get; }

    /// <summary>The warnings the engine reported while it loaded the document, as it reported them; omitted when there were none.</summary>
    [JsonPropertyOrder(860)]
    public IReadOnlyList<EngineWarning>? EngineWarnings { get; }
}

/// <summary>What an object table edit found; <see cref="TableEditResult"/> states each field.</summary>
public sealed record TableEditOutcome
{
    /// <summary>The edited document.</summary>
    public required InputInfo Input { get; init; }

    /// <summary>The written document.</summary>
    public required OutputInfo Output { get; init; }

    /// <summary>What each operation left, in batch order.</summary>
    public required IReadOnlyList<TableOpReport> Ops { get; init; }

    /// <summary>The inventory counts that changed, each as [before, after].</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<long>> Counts { get; init; }

    /// <summary>The engine's warnings, or null when it reported none.</summary>
    public IReadOnlyList<EngineWarning>? EngineWarnings { get; init; }

    /// <summary>The CLI's own warnings about the documents, such as left-out external resources, or null.</summary>
    public IReadOnlyList<Warning>? Warnings { get; init; }

    /// <summary>The license mode when the engine ran without a license.</summary>
    public LicenseInfo? License { get; init; }
}

/// <summary>
/// Base of the result of a product's <c>edit</c> on the object table. The fields are stated here
/// once for every product; a product's record names its schema and passes what the edit found.
/// </summary>
public abstract record TableEditResult : EngineResultEnvelope
{
    /// <summary>States what an edit found, under the product's schema <paramref name="schema"/>.</summary>
    /// <param name="schema">The schema id relative to the product, such as <c>edit-result</c>.</param>
    /// <param name="found">What the edit found.</param>
    /// <exception cref="ArgumentException"><paramref name="schema"/> is not a relative schema id.</exception>
    protected TableEditResult(string schema, TableEditOutcome found)
        : base(schema)
    {
        ArgumentNullException.ThrowIfNull(found);
        Input = found.Input;
        Output = found.Output;
        Ops = found.Ops;
        Counts = found.Counts;
        EngineWarnings = found.EngineWarnings;
        License = found.License;
        Warnings = found.Warnings;
    }

    /// <summary>The edited document.</summary>
    [JsonPropertyOrder(-50)]
    public InputInfo Input { get; }

    /// <summary>The written document.</summary>
    public OutputInfo Output { get; }

    /// <summary>What each operation left, in batch order: the object at its address in the saved file, or what its action returned; last, since it grows with the batch.</summary>
    [JsonPropertyOrder(1000)]
    public IReadOnlyList<TableOpReport> Ops { get; }

    /// <summary>The inventory counts that changed, each as [before, after]; the keys are those read reports in inventory.</summary>
    [JsonPropertyOrder(850)]
    public IReadOnlyDictionary<string, IReadOnlyList<long>> Counts { get; }

    /// <summary>The warnings the engine reported while it loaded, saved and reopened the document, as it reported them; omitted when there were none.</summary>
    [JsonPropertyOrder(860)]
    public IReadOnlyList<EngineWarning>? EngineWarnings { get; }
}
