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
        SkillFrontMatter? skill = SkillFrontMatter.Read(assembly, DistributionInfo.SkillPrefix + productId);

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
            skill?.Name,
            skill?.Description);
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
}
