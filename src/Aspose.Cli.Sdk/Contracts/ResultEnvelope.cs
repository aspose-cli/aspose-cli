using System.Text.Json.Serialization;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// Base of every successful command result. The envelope carries the fields
/// that are identical across all products and commands: which schema the
/// payload follows, the license mode the operation ran under, and any
/// warnings the caller must not ignore.
/// </summary>
/// <remarks>
/// Property order is fixed so output stays byte-for-byte deterministic:
/// schema identification first, command payload in the middle (order 0..n,
/// declared by derived records), then the window of a bounded read, and
/// license and warnings last. Schema identity is injected through the
/// constructor (instead of abstract properties) so the ordering attributes
/// live in exactly one place.
/// </remarks>
public abstract record ResultEnvelope
{
    protected ResultEnvelope(string schema, int schemaVersion)
    {
        ArgumentException.ThrowIfNullOrEmpty(schema);
        ArgumentOutOfRangeException.ThrowIfLessThan(schemaVersion, 1);
        Schema = schema;
        SchemaVersion = schemaVersion;
    }

    /// <summary>URI of the JSON schema this result conforms to.</summary>
    [JsonPropertyOrder(-100)]
    public string Schema { get; }

    /// <summary>Version of this result contract.</summary>
    [JsonPropertyOrder(-99)]
    public int SchemaVersion { get; }

    /// <summary>
    /// The window of a bounded read or search: how much it returned and how to continue.
    /// Omitted for results that are not windowed.
    /// </summary>
    [JsonPropertyOrder(800)]
    public ResultWindow? Window { get; init; }

    /// <summary>
    /// License mode the operation ran under. Omitted for commands that never
    /// touch the engine (for example <c>capabilities</c>).
    /// </summary>
    [JsonPropertyOrder(900)]
    public LicenseInfo? License { get; init; }

    /// <summary>
    /// Warnings the agent is expected to act on (for example
    /// <c>EVAL_MODE</c>: tell the user about the evaluation watermark).
    /// Omitted when empty.
    /// </summary>
    [JsonPropertyOrder(901)]
    public IReadOnlyList<Warning>? Warnings { get; init; }
}
