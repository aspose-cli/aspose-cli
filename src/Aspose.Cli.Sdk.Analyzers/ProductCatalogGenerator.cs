using System.Text;
using Microsoft.CodeAnalysis.Text;
using CSharpDisplay = Microsoft.CodeAnalysis.CSharp.SymbolDisplay;
using ModuleExport = (
    string ProductId,
    Microsoft.CodeAnalysis.INamedTypeSymbol? ModuleType,
    string ProductVersion,
    string SdkVersion,
    int ExportCount);

namespace Aspose.Cli.Sdk.Analyzers;

[Generator]
public sealed class ProductCatalogGenerator : IIncrementalGenerator
{
    private const string AttributeName =
        "Aspose.Cli.Sdk.Extensibility.ProductModuleAttribute";
    private const string ModuleInterfaceName =
        "Aspose.Cli.Sdk.Extensibility.IProductModule";
    private const string SdkAssemblyName = "Aspose.Cli.Sdk";

    private static readonly DiagnosticDescriptor DuplicateProductId = Rule(
        "APCLI001",
        "Duplicate product id",
        "Product id '{0}' is exported by both '{1}' and '{2}'");
    private static readonly DiagnosticDescriptor InvalidModuleType = Rule(
        "APCLI002",
        "Invalid product module type",
        "Product module '{0}' must be public, non-abstract, implement "
            + "IProductModule, and expose a public parameterless constructor");
    private static readonly DiagnosticDescriptor ModuleCountInvalid = Rule(
        "APCLI004",
        "Product assembly must export exactly one module",
        "Product assembly '{0}' exports {1} ProductModule attributes; "
            + "exactly one is required");

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValueProvider<(
            ImmutableArray<ModuleExport> Modules,
            string HostSdkVersion)> exports =
            context.CompilationProvider.Select(
                static (compilation, _) => Discover(compilation));

        context.RegisterSourceOutput(
            exports,
            static (production, modules) => Emit(production, modules));
    }

    private static (
        ImmutableArray<ModuleExport> Modules,
        string HostSdkVersion) Discover(Compilation compilation)
    {
        var modules = ImmutableArray.CreateBuilder<ModuleExport>();
        IEnumerable<IAssemblySymbol> assemblies = compilation.References
            .Select(compilation.GetAssemblyOrModuleSymbol)
            .OfType<IAssemblySymbol>()
            .Append(compilation.Assembly);

        foreach (IAssemblySymbol assembly in assemblies)
        {
            int moduleCount = 0;
            foreach (AttributeData attribute in assembly.GetAttributes())
            {
                if (!string.Equals(
                        attribute.AttributeClass?.ToDisplayString(),
                        AttributeName,
                        StringComparison.Ordinal)
                    || attribute.ConstructorArguments.Length != 2
                    || attribute.ConstructorArguments[0].Value is not string productId
                    || attribute.ConstructorArguments[1].Value is not INamedTypeSymbol moduleType)
                {
                    continue;
                }

                moduleCount++;
                string productVersion =
                    moduleType.ContainingAssembly.Identity.Version.ToString();
                string sdkVersion = FindSdkVersion(
                    moduleType.ContainingAssembly);
                modules.Add((
                    productId,
                    moduleType,
                    productVersion,
                    sdkVersion,
                    1));
            }

            if (assembly.Name.StartsWith(
                    "Aspose.Cli.Product.",
                    StringComparison.Ordinal)
                && !assembly.Name.EndsWith(
                    ".Tests",
                    StringComparison.Ordinal)
                && moduleCount != 1)
            {
                modules.Add((
                    assembly.Name,
                    null,
                    string.Empty,
                    string.Empty,
                    moduleCount));
            }
        }

        INamedTypeSymbol? moduleInterface =
            compilation.GetTypeByMetadataName(ModuleInterfaceName);
        string hostSdkVersion =
            moduleInterface?.ContainingAssembly.Identity.Version.ToString()
            ?? "0.0.0.0";
        return (modules.ToImmutable(), hostSdkVersion);
    }

    private static void Emit(
        SourceProductionContext context,
        (
            ImmutableArray<ModuleExport> Modules,
            string HostSdkVersion) catalog)
    {
        ModuleExport[] ordered = catalog.Modules
            .OrderBy(static module => module.ProductId, StringComparer.Ordinal)
            .ThenBy(
                static module =>
                    module.ModuleType?.ToDisplayString()
                    ?? string.Empty,
                StringComparer.Ordinal)
            .ToArray();
        ModuleExport[] valid = ValidateModules(context, ordered);
        string source = BuildSource(catalog.HostSdkVersion, valid);
        context.AddSource(
            "CompiledProductCatalog.g.cs",
            SourceText.From(source, Encoding.UTF8));
    }

    private static ModuleExport[] ValidateModules(
        SourceProductionContext context,
        IEnumerable<ModuleExport> modules)
    {
        var productIds = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var valid = new List<ModuleExport>();
        foreach (ModuleExport module in modules)
        {
            if (module.ModuleType is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ModuleCountInvalid,
                    Location.None,
                    module.ProductId,
                    module.ExportCount));
                continue;
            }

            if (!IsValidModule(module.ModuleType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidModuleType,
                    Location.None,
                    module.ModuleType.ToDisplayString()));
                continue;
            }
            if (!productIds.Add(module.ProductId))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateProductId,
                    Location.None,
                    module.ProductId,
                    valid.First(item => string.Equals(
                        item.ProductId,
                        module.ProductId,
                        StringComparison.OrdinalIgnoreCase))
                        .ModuleType!.ToDisplayString(),
                    module.ModuleType.ToDisplayString()));
                continue;
            }
            valid.Add(module);
        }
        return valid.ToArray();
    }

    private static string BuildSource(
        string hostSdkVersion,
        IEnumerable<ModuleExport> modules)
    {
        string registrations = string.Join(
            "\n",
            modules.Select(Registration));
        return $$"""
            // <auto-generated />
            #nullable enable
            namespace Aspose.Cli.Generated;

            internal static class CompiledProductCatalog
            {
                internal const string HostSdkVersion = {{Literal(hostSdkVersion)}};

                internal static global::Aspose.Cli.Sdk.Extensibility.ProductCatalog Instance { get; } =
                    global::Aspose.Cli.Sdk.Extensibility.ProductCatalog.Build(
                    [
            {{registrations}}
                    ],
                    HostSdkVersion);
            }
            """;
    }

    private static string Registration(ModuleExport module) =>
        $$"""
                        new({{Literal(module.ProductId)}}, new global::{{module.ModuleType!.ToDisplayString(
                            SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty)}}(), {{Literal(module.ProductVersion)}}, {{Literal(module.SdkVersion)}})
                        {
                            AssemblyName = {{Literal(module.ModuleType.ContainingAssembly.Name)}},
                            ModuleTypeName = {{Literal(module.ModuleType.ToDisplayString())}},
                        },
        """;

    private static bool IsValidModule(INamedTypeSymbol moduleType) =>
        moduleType.DeclaredAccessibility == Accessibility.Public
        && !moduleType.IsAbstract
        && moduleType.AllInterfaces.Any(static contract =>
            contract.ToDisplayString() == ModuleInterfaceName)
        && moduleType.InstanceConstructors.Any(static constructor =>
                constructor.DeclaredAccessibility == Accessibility.Public
                && constructor.Parameters.Length == 0);

    private static string FindSdkVersion(IAssemblySymbol assembly) =>
        assembly.Modules
            .SelectMany(static module =>
                module.ReferencedAssemblySymbols)
            .FirstOrDefault(static reference =>
                reference.Name == SdkAssemblyName)
            ?.Identity.Version.ToString()
        ?? "0.0.0.0";

    private static string Literal(string value) =>
        CSharpDisplay.FormatLiteral(value, quote: true);

    private static DiagnosticDescriptor Rule(
        string id,
        string title,
        string message) =>
        new(
            id,
            title,
            message,
            "Aspose.Cli.ProductDiscovery",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

}
