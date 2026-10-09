using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// Base of every successful command result. The envelope carries the fields
/// that are identical across all products and commands: which schema the
/// payload follows and any warnings the caller must not ignore. A result whose
/// command runs a product engine derives from <see cref="EngineResultEnvelope"/>,
/// and a bounded read or search from <see cref="WindowedResultEnvelope"/>, so each
/// result's schema publishes only the members it can carry.
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
    /// contract generator publishes the record's schema under the same id.
    /// </param>
    /// <param name="schemaVersion">The version of the result contract.</param>
    /// <exception cref="ArgumentException"><paramref name="schema"/> is not a relative schema id.</exception>
    protected ResultEnvelope(string schema, int schemaVersion)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(schemaVersion, 1);
        Schema = SchemaUri(OwnerOf(GetType().Assembly), schema);
        SchemaVersion = schemaVersion;
    }

    /// <summary>URI of the JSON schema this result conforms to.</summary>
    [JsonPropertyOrder(-100)]
    public string Schema { get; }

    /// <summary>Version of this result contract.</summary>
    [JsonPropertyOrder(-99)]
    public int SchemaVersion { get; }

    /// <summary>
    /// Warnings the agent is expected to act on (for example
    /// <c>EVAL_MODE</c>: tell the user about the evaluation watermark).
    /// Omitted when empty.
    /// </summary>
    [JsonPropertyOrder(901)]
    public IReadOnlyList<Warning>? Warnings { get; init; }

    /// <summary>The canonical URI of the schema <paramref name="id"/> of <paramref name="owner"/>.</summary>
    /// <param name="owner">A product id, or <c>common</c>.</param>
    /// <param name="id">The relative schema id, such as <c>render-result</c>.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is not a relative schema id.</exception>
    public static string SchemaUri(string owner, string id) =>
        id is not null && RelativeId().IsMatch(id)
            ? $"{DistributionInfo.SchemaBaseUri}{owner}/{id}.schema.json"
            : throw new ArgumentException($"'{id}' is not a relative schema id: lower-case words joined by hyphens, such as 'render-result'.", nameof(id));

    /// <summary>
    /// The id the schema catalog names the schema <paramref name="id"/> of <paramref name="owner"/> by,
    /// such as <c>v2/common/backup</c>.
    /// </summary>
    /// <param name="owner">A product id, or <c>common</c>.</param>
    /// <param name="id">The relative schema id, such as <c>backup</c>.</param>
    public static string CatalogId(string owner, string id) => $"v2/{owner}/{id}";

    /// <summary>
    /// The owner of the schemas an assembly's records state: the product id its product module
    /// declares, or <c>common</c>. The declaration is read by name, because the product module
    /// attribute belongs to a higher SDK layer.
    /// </summary>
    internal static string OwnerOf(Assembly assembly) =>
        Owners.GetOrAdd(assembly, static assembly => assembly.GetCustomAttributesData()
            .Where(static attribute => attribute.AttributeType.FullName == "Aspose.Cli.Sdk.Extensibility.ProductModuleAttribute")
            .Select(static attribute => attribute.ConstructorArguments[0].Value as string)
            .FirstOrDefault() ?? "common");

    /// <summary>A relative schema id: lower-case words joined by hyphens.</summary>
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    internal static partial Regex RelativeId();
}
