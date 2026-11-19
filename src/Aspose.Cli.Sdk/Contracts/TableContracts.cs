using System.Text.Json.Nodes;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>One object of a document, as the object table reads it.</summary>
[SchemaId("table-object")]
public sealed record TableObject
{
    /// <summary>The object's address, such as slide/256/shape/3; edit and read --at take it as it is.</summary>
    public required string At { get; init; }

    /// <summary>The object's kind, one of the kinds the product's ops schema lists.</summary>
    public required string Kind { get; init; }

    /// <summary>Every row of the table the object has, by row name, as the engine gives it.</summary>
    public required JsonObject Props { get; init; }
}

/// <summary>
/// What one operation of an edit left, read from the saved file after it was opened again: the
/// object at the operation's address, or the value an action's engine call returned.
/// </summary>
[SchemaId("table-op-report")]
public sealed record TableOpReport
{
    /// <summary>
    /// The address the operation named or created, in its canonical form; omitted for an action
    /// that returns no object. A position in it (a paragraph's, a run's, a cell's, a series', a
    /// point's) is the position the object had when the operation ran; a later operation of the
    /// batch can move what is at that position.
    /// </summary>
    public string? At { get; init; }

    /// <summary>The action the operation ran; omitted for set, add, delete and move.</summary>
    public string? Do { get; init; }

    /// <summary>
    /// Whether an object is at the address in the saved file; omitted where there is no address.
    /// An address by id is false after a delete that removed it. An address by position names the
    /// position: after a delete, true with the rows of the object that moved into it, false only
    /// when nothing is there; neither says whether the delete removed what it named.
    /// </summary>
    public bool? Exists { get; init; }

    /// <summary>
    /// Every row of the object that is at the address in the saved file, after the whole batch;
    /// omitted when no object is there. At a position, this is what the saved file holds there,
    /// which a later operation of the batch may have put there.
    /// </summary>
    public JsonObject? Now { get; init; }

    /// <summary>The value the action's engine call returned; omitted for set, add, delete and move and for a call that returns nothing.</summary>
    public JsonNode? Returned { get; init; }
}

/// <summary>A warning the document engine reported through its warning callback, as it reported it.</summary>
[SchemaId("engine-warning")]
public sealed record EngineWarning
{
    /// <summary>When the engine reported it: load (opening the input), save (writing the output) or reopen (opening the saved output again).</summary>
    [AllowedValues("load", "save", "reopen")]
    public required string During { get; init; }

    /// <summary>The engine's warning type, by its member name.</summary>
    public required string Type { get; init; }

    /// <summary>The engine's description, as it is.</summary>
    public required string Description { get; init; }
}

/// <summary>One match of a search in a text row of the object table.</summary>
[SchemaId("table-hit")]
public sealed record TableHit
{
    /// <summary>The address of the object whose row holds the match.</summary>
    public required string At { get; init; }

    /// <summary>The text row that holds the match, such as text.</summary>
    public required string Row { get; init; }

    /// <summary>Where the match starts in the row's text, counted in UTF-16 code units from 0.</summary>
    [Minimum(0)]
    public required int Offset { get; init; }

    /// <summary>The match's length in UTF-16 code units.</summary>
    [Minimum(0)]
    public required int Length { get; init; }

    /// <summary>The matched text, as the row holds it.</summary>
    public required string Text { get; init; }
}

/// <summary>
/// How many objects of each kind of content a document holds, counted with the engine's own
/// collections: <c>read</c> for the kinds the object table addresses, which read lists, and
/// <c>notRead</c> for the kinds it does not.
/// </summary>
[SchemaId("table-inventory")]
public sealed record TableInventory
{
    /// <summary>The counts of the kinds read lists, by inventory key.</summary>
    public required IReadOnlyDictionary<string, long> Read { get; init; }

    /// <summary>The counts of the kinds read does not list, by inventory key.</summary>
    public required IReadOnlyDictionary<string, long> NotRead { get; init; }
}
