using System.Reflection;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>Read-only index of resources owned by one product package.</summary>
public sealed class ProductPackageResources
{
    /// <summary>
    /// Discovers convention-based product resources and validates Skill front
    /// matter without requiring paths in product module code.
    /// </summary>
    internal static ProductPackageResources Discover(
        Assembly assembly,
        string productId)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        string[] names = assembly.GetManifestResourceNames();
        string expectedSkill = "aspose-cli-" + productId;
        string skillResource = $"skill/{expectedSkill}/SKILL.md";
        string? skillName = null;
        string? skillDescription = null;
        if (names.Contains(
                skillResource,
                StringComparer.Ordinal))
        {
            string frontMatter = Read(assembly, skillResource);
            (skillName, skillDescription) = ParseFrontMatter(frontMatter);
            if (!string.Equals(
                    skillName,
                    expectedSkill,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Product '{productId}' Skill name must be '{expectedSkill}', not '{skillName}'.");
            }
            if (string.IsNullOrWhiteSpace(skillDescription))
            {
                throw new InvalidOperationException(
                    $"Product '{productId}' Skill front matter has no description.");
            }
        }

        return new ProductPackageResources(
            productId,
            assembly,
            Array.AsReadOnly(names),
            Array.AsReadOnly(
                names.Select(ProductResourceCatalog.TryGetSchemaId)
                    .Where(static id => id is not null)
                    .Cast<string>()
                    .Order(StringComparer.Ordinal)
                    .ToArray()),
            skillName,
            skillDescription);
    }

    private ProductPackageResources(
        string productId,
        Assembly resourceAssembly,
        IReadOnlyList<string> resourceNames,
        IReadOnlyList<string> schemaIds,
        string? skillName,
        string? skillDescription)
    {
        ProductId = productId;
        ResourceAssembly = resourceAssembly;
        ResourceNames = resourceNames;
        SchemaIds = schemaIds;
        SkillName = skillName;
        SkillDescription = skillDescription;
    }

    /// <summary>Stable product id that owns every indexed resource.</summary>
    public string ProductId { get; }

    /// <summary>Assembly containing the declared embedded resources.</summary>
    public Assembly ResourceAssembly { get; }

    /// <summary>Embedded resource names captured by the catalog's single scan.</summary>
    public IReadOnlyList<string> ResourceNames { get; }

    /// <summary>Schema identifiers owned by this package.</summary>
    public IReadOnlyList<string> SchemaIds { get; }

    /// <summary>Agent Skill package name, when the product ships one.</summary>
    public string? SkillName { get; }

    /// <summary>Human-readable Agent Skill summary.</summary>
    public string? SkillDescription { get; }

    private static string Read(Assembly assembly, string name)
    {
        using Stream stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException(
                $"Embedded product resource '{name}' is unavailable.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static (string? Name, string? Description) ParseFrontMatter(
        string document)
    {
        using var reader = new StringReader(document);
        if (!string.Equals(reader.ReadLine(), "---", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Product Skill must start with YAML front matter.");
        }

        string? name = null;
        string? description = null;
        for (string? line = reader.ReadLine();
            line is not null && line != "---";
            line = reader.ReadLine())
        {
            int separator = line.IndexOf(':');
            if (separator < 1)
            {
                continue;
            }
            string key = line[..separator].Trim();
            string value = line[(separator + 1)..].Trim();
            if (key == "name")
            {
                name = value;
            }
            else if (key == "description")
            {
                description = value;
            }
        }
        return (name, description);
    }
}
