using System.Text.Json.Serialization;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// Base of the result of a bounded read or search, which always states its window: how much it
/// returned and the option values that continue it.
/// </summary>
public abstract record WindowedResultEnvelope : EngineResultEnvelope
{
    /// <summary>Starts a result that follows the schema <paramref name="schema"/> and states <paramref name="window"/>.</summary>
    /// <param name="schema">The schema id relative to the record's owner, such as <c>read-result</c>.</param>
    /// <param name="window">The window of the read or search.</param>
    /// <exception cref="ArgumentException"><paramref name="schema"/> is not a relative schema id.</exception>
    protected WindowedResultEnvelope(string schema, ResultWindow window)
        : base(schema)
    {
        Window = window ?? throw new ArgumentNullException(nameof(window));
    }

    /// <summary>The window of the read or search: how much it returned and the option values that continue it.</summary>
    [JsonPropertyOrder(800)]
    public ResultWindow Window { get; }
}
