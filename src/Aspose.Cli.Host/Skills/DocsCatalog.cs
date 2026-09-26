using System.Reflection;
using Aspose.Cli.Sdk;

namespace Aspose.Cli.Host.Skills;

/// <summary>
/// Serves every embedded Agent Skill as offline documentation. The platform Skill's documents
/// are the unprefixed topics (<c>overview</c>, <c>licensing</c>, ...); a product Skill's are
/// <c>&lt;product&gt;/&lt;topic&gt;</c>. A reference is named after its file and an example after its
/// directory; <c>overview</c> is the Skill's SKILL.md.
/// </summary>
internal sealed class DocsCatalog
{
    private const string MarkdownExtension = ".md";
    private readonly IReadOnlyDictionary<string, DocResource> _resourceByTopic;

    public DocsCatalog(SkillCatalog skills)
    {
        ArgumentNullException.ThrowIfNull(skills);
        _resourceByTopic = BuildMap(skills);
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

    private static Dictionary<string, DocResource> BuildMap(SkillCatalog skills)
    {
        var map = new Dictionary<string, DocResource>(StringComparer.Ordinal);
        foreach (BundledSkill skill in skills.All)
        {
            string topicPrefix = skill.Name == SkillCatalog.PlatformSkillName
                ? string.Empty
                : skill.Name[DistributionInfo.SkillPrefix.Length..] + "/";
            string root = $"skill/{skill.Name}/";
            string references = $"{root}references/";
            string examples = $"{root}examples/";
            foreach (string resource in skill.ResourceNames)
            {
                string normalized = resource.Replace('\\', '/');
                var location = new DocResource(skill.ResourceAssembly, resource);
                if (normalized.StartsWith(references, StringComparison.Ordinal)
                    && normalized.EndsWith(MarkdownExtension, StringComparison.Ordinal))
                {
                    map[topicPrefix + normalized[references.Length..^MarkdownExtension.Length]] = location;
                }
                else if (normalized.StartsWith(examples, StringComparison.Ordinal)
                    && normalized.EndsWith("/README.md", StringComparison.Ordinal))
                {
                    map[topicPrefix + normalized[examples.Length..^"/README.md".Length]] = location;
                }
                else if (string.Equals(normalized, $"{root}SKILL.md", StringComparison.Ordinal))
                {
                    map[topicPrefix + "overview"] = location;
                }
            }
        }

        return map;
    }

    private sealed record DocResource(Assembly Assembly, string Name);
}
