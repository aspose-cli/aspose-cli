using Aspose.Cli.Host.Skills;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Host.Invocation;

/// <summary>Immutable resources owned by one statically composed CLI host.</summary>
internal sealed class HostContext
{
    public HostContext(ProductCatalog catalog, WorkerOutputSession? workerOutputs = null)
    {
        WorkerOutputs = workerOutputs;
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _parser = new Lazy<InvocationParser>(() =>
        {
            var root = Commands.RootCommandFactory.Create(this, out GlobalOptions globals);
            return new InvocationParser(root, globals);
        });
        ContractJson = new ContractJsonSerializer(catalog.JsonDefinitions);
        Schemas = new Aspose.Cli.Host.Commands.HostSchemaCatalog(catalog);
        Skills = new SkillCatalog(catalog);
        Docs = new DocsCatalog(Skills);
        EngineFailures = EngineFailureTranslator.Create(catalog);
    }

    private readonly Lazy<InvocationParser> _parser;

    internal InvocationParser Parser => _parser.Value;

    public ProductCatalog Catalog { get; }
    public WorkerOutputSession? WorkerOutputs { get; }

    public ContractJsonSerializer ContractJson { get; }

    public Aspose.Cli.Host.Commands.HostSchemaCatalog Schemas { get; }

    public DocsCatalog Docs { get; }

    public SkillCatalog Skills { get; }

    internal EngineFailureTranslator EngineFailures { get; }
}
