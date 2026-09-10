using System.Reflection;

namespace Aspose.Cli.Sdk.Resources;

/// <summary>
/// Read access to the JSON Schema documents embedded in this assembly.
/// Schemas are the single source of truth of the wire contract; the
/// <c>aspose-cli schema</c> command prints them and contract tests validate
/// golden payloads against them.
/// </summary>
public static class SdkSchemaCatalog
{
    private const string ResourcePrefix = "schemas/";
    private const string ResourceSuffix = ".schema.json";

    /// <summary>
    /// All schema ids in this build, e.g. <c>v2/common/error</c>. Derived from
    /// the embedded resources so the list can never go stale.
    /// </summary>
    public static IReadOnlyList<string> Ids { get; } = DiscoverIds();

    /// <summary>Returns the schema document for an id from <see cref="Ids"/>.</summary>
    /// <exception cref="ArgumentException">The id is not in the catalog.</exception>
    public static string Read(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        return TryRead(id, out string? schema)
            ? schema
            : throw new ArgumentException($"Unknown schema id '{id}'. Known ids: {string.Join(", ", Ids)}", nameof(id));
    }

    /// <summary>Attempts to read the schema document for an id.</summary>
    public static bool TryRead(string id, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? schema)
    {
        schema = null;
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        using Stream? stream = typeof(SdkSchemaCatalog).Assembly
            .GetManifestResourceStream(ResourcePrefix + id + ResourceSuffix);
        if (stream is null)
        {
            return false;
        }

        using var reader = new StreamReader(stream);
        schema = reader.ReadToEnd();
        return true;
    }

    private static string[] DiscoverIds()
    {
        Assembly assembly = typeof(SdkSchemaCatalog).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(static name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                && name.EndsWith(ResourceSuffix, StringComparison.Ordinal))
            .Select(static name => name[ResourcePrefix.Length..^ResourceSuffix.Length])
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();
    }
}
