using System.Reflection;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Host.Skills;

/// <summary>Serves every embedded product skill as offline documentation.</summary>
internal sealed class DocsCatalog
{
    private const string MarkdownExtension = ".md";
    private readonly IReadOnlyDictionary<string, DocResource> _resourceByTopic;

    public DocsCatalog(ProductCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _resourceByTopic = BuildMap(catalog);
        Topics = _resourceByTopic.Keys
            .OrderBy(static topic => topic, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<string> Topics { get; }

    public bool TryRead(string topic, out string content)
    {
        ArgumentNullException.ThrowIfNull(topic);
        if (_resourceByTopic.TryGetValue(topic, out DocResource? resource))
        {
            using Stream stream = resource.Assembly.GetManifestResourceStream(resource.Name)!;
            using var reader = new StreamReader(stream);
            content = reader.ReadToEnd();
            return true;
        }

        content = string.Empty;
        return false;
    }

    private static Dictionary<string, DocResource> BuildMap(ProductCatalog catalog)
    {
        var map = new Dictionary<string, DocResource>(StringComparer.Ordinal);
        foreach (ProductPackageResources resources
            in catalog.Resources.Products)
        {
            if (resources.SkillName is not { } skillName)
            {
                continue;
            }

            Assembly resourceAssembly = resources.ResourceAssembly;
            string productId = resources.ProductId;
            string root = $"skill/{skillName}/";
            string references = $"{root}references/";
            string examples = $"{root}examples/";
            foreach (string resource in resources.ResourceNames)
            {
                string normalized = resource.Replace('\\', '/');
                var location = new DocResource(resourceAssembly, resource);
                if (normalized.StartsWith(references, StringComparison.Ordinal)
                    && normalized.EndsWith(MarkdownExtension, StringComparison.Ordinal))
                {
                    string topic = normalized[references.Length..^MarkdownExtension.Length];
                    map[$"{productId}/{topic}"] = location;
                }
                else if (normalized.StartsWith(examples, StringComparison.Ordinal)
                    && normalized.EndsWith("/README.md", StringComparison.Ordinal))
                {
                    string topic = normalized[examples.Length..^"/README.md".Length];
                    map[$"{productId}/{topic}"] = location;
                }
                else if (string.Equals(normalized, $"{root}SKILL.md", StringComparison.Ordinal))
                {
                    map[$"{productId}/overview"] = location;
                }
            }

            if (map.TryGetValue($"{productId}/editing", out DocResource? editing))
            {
                map[$"{productId}/ops"] = editing;
            }
        }

        string? defaultProductId = catalog.DefaultProduct?.Manifest.Id;
        if (defaultProductId is not null)
        {
            foreach ((string topic, DocResource resource) in map
                         .Where(pair => pair.Key.StartsWith(
                             defaultProductId + "/",
                             StringComparison.Ordinal))
                         .ToArray())
            {
                map[topic[(defaultProductId.Length + 1)..]] = resource;
            }
        }

        return map;
    }

    private sealed record DocResource(Assembly Assembly, string Name);
}
