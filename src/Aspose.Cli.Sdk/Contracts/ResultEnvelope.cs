using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

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
public abstract partial record ResultEnvelope
{
    private static readonly ConcurrentDictionary<Assembly, string> Owners = new();

    /// <summary>Starts a result that follows the schema <paramref name="schema"/>.</summary>
    /// <param name="schema">
    /// The schema id relative to the record's owner, such as <c>render-result</c>: the product
    /// whose assembly declares the record, or <c>common</c> for the SDK and the host. The
    /// contract generator publishes the record's schema under the same id. Any other value, such
    /// as a full schema URI, is taken as it is and publishes no schema.
    /// </param>
    /// <param name="schemaVersion">The version of the result contract.</param>
    protected ResultEnvelope(string schema, int schemaVersion)
    {
        ArgumentException.ThrowIfNullOrEmpty(schema);
        ArgumentOutOfRangeException.ThrowIfLessThan(schemaVersion, 1);
        Schema = RelativeId().IsMatch(schema) ? SchemaUri(Owner(GetType().Assembly), schema) : schema;
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

    /// <summary>The canonical URI of the schema <paramref name="id"/> of <paramref name="owner"/>.</summary>
    /// <param name="owner">A product id, or <c>common</c>.</param>
    /// <param name="id">The relative schema id.</param>
    public static string SchemaUri(string owner, string id) => $"{DistributionInfo.SchemaBaseUri}{owner}/{id}.schema.json";

    /// <summary>
    /// The product id an assembly's product module declares, or <c>common</c>. The declaration is
    /// read by name, because the product module attribute belongs to a higher SDK layer.
    /// </summary>
    private static string Owner(Assembly assembly) =>
        Owners.GetOrAdd(assembly, static assembly => assembly.GetCustomAttributesData()
            .Where(static attribute => attribute.AttributeType.FullName == "Aspose.Cli.Sdk.Extensibility.ProductModuleAttribute")
            .Select(static attribute => attribute.ConstructorArguments[0].Value as string)
            .FirstOrDefault() ?? "common");

    /// <summary>A relative schema id: lower-case words joined by hyphens.</summary>
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    internal static partial Regex RelativeId();
}
