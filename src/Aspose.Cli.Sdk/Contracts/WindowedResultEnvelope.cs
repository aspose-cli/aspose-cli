using System.Text.Json.Serialization;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// Base of the result of a bounded read or search, which always states its window: how much it
/// returned and the command that continues it.
/// </summary>
public abstract record WindowedResultEnvelope : EngineResultEnvelope
{
    /// <summary>Starts a result that follows the schema <paramref name="schema"/>.</summary>
    /// <param name="schema">The schema id relative to the record's owner, such as <c>read-result</c>.</param>
    /// <exception cref="ArgumentException"><paramref name="schema"/> is not a relative schema id.</exception>
    protected WindowedResultEnvelope(string schema)
        : base(schema)
    {
    }

    /// <summary>The window of the read or search: how much it returned and how to continue.</summary>
    [JsonPropertyOrder(800)]
    public required ResultWindow Window { get; init; }
}
