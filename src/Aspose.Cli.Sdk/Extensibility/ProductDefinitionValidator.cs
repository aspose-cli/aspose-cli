using System.CommandLine;
using Aspose.Cli.Sdk.Formats;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>Validates discovered product identity, contracts, formats, and resources.</summary>
internal sealed class ProductDefinitionValidator
{
    private readonly IReadOnlyDictionary<IProductModule, ProductModuleRegistration>?
        _descriptors;

    public ProductDefinitionValidator(
        IReadOnlyDictionary<IProductModule, ProductModuleRegistration>? descriptors) =>
        _descriptors = descriptors;

    public ProductDefinition Prepare(IProductModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        ProductDefinition definition = module.Define()
            ?? throw new InvalidOperationException(
                $"Product module '{module.GetType().FullName}' returned null.");
        if (_descriptors is not null
            && _descriptors.TryGetValue(module, out ProductModuleRegistration? descriptor))
        {
            ValidateDescriptor(descriptor, module, definition);
        }

        ValidateManifest(definition.Manifest);
        ValidateFormats(definition);
        ValidateBudgetOptions(definition);
        return definition;
    }

    public void ValidateResources(
        ProductDefinition definition,
        ProductPackageResources resources)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(resources);
        string productId = definition.Manifest.Id;
        if (!string.Equals(
                productId,
                resources.ProductId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Product '{productId}' received resources for '{resources.ProductId}'.");
        }
    }

    public static IReadOnlyDictionary<IProductModule, ProductModuleRegistration>
        ValidateDiscovery(
            IReadOnlyList<ProductModuleRegistration> registrations,
            string hostSdkVersion)
    {
        Version hostVersion = ParseVersion(hostSdkVersion, "host SDK");
        var byModule = new Dictionary<IProductModule, ProductModuleRegistration>(
            ReferenceEqualityComparer.Instance);
        foreach (ProductModuleRegistration descriptor in registrations)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            ArgumentNullException.ThrowIfNull(descriptor.Module);
            Version productSdk = ParseVersion(
                descriptor.SdkVersion,
                $"product '{descriptor.ProductId}' SDK");
            if (productSdk.Major != hostVersion.Major)
            {
                throw new InvalidOperationException(
                    $"Product '{descriptor.ProductId}' version "
                    + $"'{descriptor.ProductVersion}' targets Aspose.Cli.Sdk "
                    + $"'{descriptor.SdkVersion}', but the host uses "
                    + $"'{hostSdkVersion}'. Upgrade the product package or host "
                    + "so their SDK major versions match.");
            }
            if (!byModule.TryAdd(descriptor.Module, descriptor))
            {
                throw new InvalidOperationException(
                    $"Product module '{descriptor.Module.GetType().FullName}' has more than one discovery descriptor.");
            }
        }

        return byModule;
    }

    private static void ValidateManifest(ProductManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (string.IsNullOrWhiteSpace(manifest.Id)
            || manifest.Id.Any(static character =>
                !(char.IsAsciiLetterOrDigit(character)
                    || character is '-' or '_'))
            || manifest.Id != manifest.Id.ToLowerInvariant())
        {
            throw new InvalidOperationException(
                $"Product id '{manifest.Id}' must contain only lower-case ASCII letters, digits, '-' or '_'.");
        }
        if (string.IsNullOrWhiteSpace(manifest.DisplayName))
        {
            throw new InvalidOperationException(
                $"Product '{manifest.Id}' has no display name.");
        }
    }

    private static void ValidateBudgetOptions(ProductDefinition definition)
    {
        string[] declared = definition.Manifest.ResourceBudgets
            .Select(static budget => budget.Option)
            .OfType<string>()
            .ToArray();
        if (declared.Length == 0)
        {
            return;
        }

        var registered = new HashSet<string>(StringComparer.Ordinal);
        // A tree built for inspection, whose commands cannot run.
        CollectOptions(
            definition.CreateCommand(static (_, _) =>
                throw new InvalidOperationException("A command tree built for validation cannot run.")),
            registered);
        foreach (string option in declared)
        {
            if (!registered.Contains(option))
            {
                throw new InvalidOperationException(
                    $"Product '{definition.Manifest.Id}' declares a resource budget raised by '{option}', "
                    + "but no command registers that option.");
            }
        }
    }

    private static void CollectOptions(Command command, ISet<string> names)
    {
        foreach (Option option in command.Options)
        {
            names.Add(option.Name);
            foreach (string alias in option.Aliases)
            {
                names.Add(alias);
            }
        }

        foreach (Command child in command.Subcommands)
        {
            CollectOptions(child, names);
        }
    }

    private static void ValidateDescriptor(
        ProductModuleRegistration descriptor,
        IProductModule module,
        ProductDefinition definition)
    {
        Type moduleType = module.GetType();
        string actualAssembly = moduleType.Assembly.GetName().Name ?? string.Empty;
        string actualType = moduleType.FullName ?? moduleType.Name;
        if (!string.Equals(
                descriptor.ProductId,
                definition.Manifest.Id,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Discovery descriptor id '{descriptor.ProductId}' does not match manifest id '{definition.Manifest.Id}'.");
        }
        if (descriptor.AssemblyName is null && descriptor.ModuleTypeName is null)
        {
            return;
        }
        if (descriptor.AssemblyName is null || descriptor.ModuleTypeName is null)
        {
            throw new InvalidOperationException(
                $"Product '{descriptor.ProductId}' discovery descriptor must declare both assembly and module identities.");
        }
        if (!string.Equals(
                descriptor.AssemblyName,
                actualAssembly,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Product '{descriptor.ProductId}' descriptor assembly '{descriptor.AssemblyName}' does not match '{actualAssembly}'.");
        }
        if (!string.Equals(
                descriptor.ModuleTypeName,
                actualType,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Product '{descriptor.ProductId}' descriptor module '{descriptor.ModuleTypeName}' does not match '{actualType}'.");
        }

        ProductModuleAttribute[] attributes = moduleType.Assembly
            .GetCustomAttributes(typeof(ProductModuleAttribute), inherit: false)
            .Cast<ProductModuleAttribute>()
            .ToArray();
        if (attributes.Length != 1)
        {
            throw new InvalidOperationException(
                $"Product assembly '{actualAssembly}' must declare exactly one ProductModule attribute; found {attributes.Length}.");
        }
        ProductModuleAttribute attribute = attributes[0];
        if (!string.Equals(
                attribute.ProductId,
                descriptor.ProductId,
                StringComparison.Ordinal)
            || attribute.ModuleType != moduleType)
        {
            throw new InvalidOperationException(
                $"Product '{descriptor.ProductId}' assembly attribute does not match its discovery descriptor.");
        }
    }

    private static void ValidateFormats(ProductDefinition definition)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (FormatDescriptor format in definition.Formats)
        {
            if (string.IsNullOrWhiteSpace(format.Id)
                || format.Id != format.Id.ToLowerInvariant()
                || format.Id.Any(static character =>
                    !(char.IsAsciiLetterOrDigit(character)
                        || character is '-' or '_' or '.')))
            {
                throw new InvalidOperationException(
                    $"Product '{definition.Manifest.Id}' declares invalid format id '{format.Id}'.");
            }
            if (!ids.Add(format.Id))
            {
                throw new InvalidOperationException(
                    $"Product '{definition.Manifest.Id}' declares format '{format.Id}' more than once.");
            }
            if (format.Uses == 0)
            {
                throw new InvalidOperationException(
                    $"Product '{definition.Manifest.Id}' format '{format.Id}' has no use.");
            }
            foreach (string extension in format.Extensions)
            {
                _ = ProductCatalog.NormalizeExtension(extension);
            }
            if (format.Aliases.Any(string.IsNullOrWhiteSpace)
                || format.Aliases.Distinct(StringComparer.OrdinalIgnoreCase).Count()
                    != format.Aliases.Count)
            {
                throw new InvalidOperationException(
                    $"Product '{definition.Manifest.Id}' format '{format.Id}' declares invalid or duplicate aliases.");
            }
        }
    }

    private static Version ParseVersion(string value, string owner) =>
        Version.TryParse(value, out Version? version)
            ? version
            : throw new InvalidOperationException(
                $"The compile-time {owner} version '{value}' is invalid.");
}
