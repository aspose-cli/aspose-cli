using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Host.Serialization;
using Aspose.Cli.Host.Skills;

namespace Aspose.Cli.Host.Invocation;

/// <summary>Immutable resources owned by one statically composed CLI host.</summary>
internal sealed class HostContext
{
    public HostContext(ProductCatalog catalog, CliEditionInfo edition)
    {
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        Edition = edition ?? throw new ArgumentNullException(nameof(edition));
        ContractJson = new HostContractJson(catalog);
        Schemas = new Aspose.Cli.Host.Commands.HostSchemaCatalog(catalog);
        Docs = new DocsCatalog(catalog);
        Skills = new SkillCatalog(catalog);
    }

    public ProductCatalog Catalog { get; }

    public CliEditionInfo Edition { get; }

    public HostContractJson ContractJson { get; }

    public Aspose.Cli.Host.Commands.HostSchemaCatalog Schemas { get; }

    public DocsCatalog Docs { get; }

    public SkillCatalog Skills { get; }
}
