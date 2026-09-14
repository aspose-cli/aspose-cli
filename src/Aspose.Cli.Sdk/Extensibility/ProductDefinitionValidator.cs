using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>Validates discovered product identity, contracts, formats, and resources.</summary>
internal sealed class ProductDefinitionValidator
{
    private readonly IReadOnlyDictionary<IProductModule, ProductModuleRegistration>?
        _descriptors;
    private readonly Dictionary<Type, string> _outputs = [];

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
        RegisterContracts(definition);
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

        string schemaPrefix = $"v2/{productId}/";
        foreach (string schemaId in resources.SchemaIds)
        {
            if (!schemaId.StartsWith(schemaPrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Product '{productId}' schema '{schemaId}' is outside resource namespace '{schemaPrefix}'.");
            }
        }
        ValidateOperationSchemas(definition, resources.SchemaIds);
        ValidatePreviewPayloadContracts(definition, resources.SchemaIds);
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

    private void RegisterContracts(ProductDefinition definition)
    {
        string productId = definition.Manifest.Id;
        foreach (ProductOutputDefinition output in definition.Outputs)
        {
            if (_outputs.TryGetValue(output.ResultType, out string? owner))
            {
                throw new InvalidOperationException(
                    $"Result type '{output.ResultType.FullName}' has multiple renderers: "
                    + $"'{owner}' and '{productId}'.");
            }
            _outputs.Add(output.ResultType, productId);
        }
    }

    private static void ValidatePreviewPayloadContracts(
        ProductDefinition definition,
        IReadOnlyList<string> schemaIds)
    {
        ProductPreviewDefinition? preview = definition.Preview;
        if (preview is null)
        {
            return;
        }
        string productId = definition.Manifest.Id;
        if (!string.Equals(preview.ProductId, productId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Product '{productId}' preview is bound to payload owner "
                + $"'{preview.ProductId}'.");
        }

        var identities = new HashSet<string>(StringComparer.Ordinal);
        string schemaPrefix = $"v2/{productId}/";
        foreach (ProductPreviewPayloadContract contract in preview.PayloadContracts)
        {
            ArgumentNullException.ThrowIfNull(contract);
            if (string.IsNullOrWhiteSpace(contract.Kind)
                || contract.Kind.Length > 32
                || contract.Kind[0] is < 'a' or > 'z'
                || contract.Kind.Any(static character =>
                    character is not (>= 'a' and <= 'z'
                        or >= '0' and <= '9'
                        or '-')))
            {
                throw new InvalidOperationException(
                    $"Product '{productId}' preview payload kind "
                    + $"'{contract.Kind}' is invalid.");
            }
            if (contract.SchemaVersion != 2)
            {
                throw new InvalidOperationException(
                    $"Product '{productId}' preview payload "
                    + $"'{contract.Kind}' uses schema version "
                    + $"{contract.SchemaVersion}; this build supports version 2.");
            }
            if (contract.MaxBytes is < 1 or > 65_536)
            {
                throw new InvalidOperationException(
                    $"Product '{productId}' preview payload "
                    + $"'{contract.Kind}' has invalid byte limit "
                    + $"{contract.MaxBytes}.");
            }
            if (!contract.SchemaId.StartsWith(schemaPrefix, StringComparison.Ordinal)
                || !schemaIds.Contains(
                    contract.SchemaId,
                    StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Product '{productId}' preview payload "
                    + $"'{contract.Kind}@v{contract.SchemaVersion}' references "
                    + $"unowned schema '{contract.SchemaId}'.");
            }
            string identity = $"{contract.Kind}:{contract.SchemaVersion}";
            if (!identities.Add(identity))
            {
                throw new InvalidOperationException(
                    $"Product '{productId}' declares preview payload "
                    + $"'{contract.Kind}@v{contract.SchemaVersion}' more than once.");
            }
        }
    }

    private static void ValidateOperationSchemas(
        ProductDefinition definition,
        IReadOnlyList<string> schemaIds)
    {
        string productId = definition.Manifest.Id;
        string prefix = $"v2/{productId}/";
        foreach (ProductOperationDescriptor operation in
            definition.Manifest.Operations)
        {
            if (!operation.InputSchema.StartsWith(
                    prefix,
                    StringComparison.Ordinal)
                || !schemaIds.Contains(
                    operation.InputSchema,
                    StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Product '{productId}' operation '{operation.Id}' references unowned schema '{operation.InputSchema}'.");
            }
        }
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
        if (!string.Equals(manifest.ContractVersion, "v1", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Product '{manifest.Id}' targets unsupported contract '{manifest.ContractVersion}'.");
        }
        ValidateOperations(manifest);
        if (string.IsNullOrWhiteSpace(manifest.Engine.Id)
            || !manifest.AvailableEngines.Contains(
                manifest.Engine.Id,
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Product '{manifest.Id}' default engine must be listed as available.");
        }
    }

    private static void ValidateOperations(ProductManifest manifest)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (ProductOperationDescriptor? operation in
            manifest.Operations)
        {
            if (operation is null
                || !IsToken(operation.Id)
                || !ids.Add(operation.Id))
            {
                throw new InvalidOperationException(
                    $"Product '{manifest.Id}' declares an invalid or duplicate operation id.");
            }
            string[] command = operation.Command.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);
            if (command.Length == 0
                || command.Any(static segment => !IsToken(segment))
                || !string.Equals(
                    operation.Command,
                    string.Join(' ', command),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Product '{manifest.Id}' operation '{operation.Id}' declares invalid command path '{operation.Command}'.");
            }
            if (string.IsNullOrWhiteSpace(operation.InputSchema))
            {
                throw new InvalidOperationException(
                    $"Product '{manifest.Id}' operation '{operation.Id}' has no input schema.");
            }
        }
    }

    private static bool IsToken(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value[0] is >= 'a' and <= 'z'
        && value.All(static character =>
            character is >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '-' or '_');

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
            if (format.Operations.Any(string.IsNullOrWhiteSpace)
                || format.Operations.Distinct(StringComparer.Ordinal).Count()
                    != format.Operations.Count)
            {
                throw new InvalidOperationException(
                    $"Product '{definition.Manifest.Id}' format '{format.Id}' declares invalid or duplicate operations.");
            }
        }

        if (definition.Formats.Count > 0
            && definition.Files.DefaultOwnerExtensions.Count > 0
            && definition.Files.Recognizer is null)
        {
            throw new InvalidOperationException(
                $"Product '{definition.Manifest.Id}' claims generic routes without a bounded content recognizer.");
        }
        if (definition.Formats.Count > 0
            && definition.Files.DefaultOwnerExtensions.Count > 0
            && definition.Files.Recognizer is { } recognizer
            && !recognizer.Descriptor.CooperativeCancellation)
        {
            throw new InvalidOperationException(
                $"Product '{definition.Manifest.Id}' generic recognizer "
                + $"'{recognizer.Descriptor.Strategy}' is not approved for cooperative in-process cancellation. "
                + "Keep the format explicit-only or provide a terminable worker boundary.");
        }
    }

    private static Version ParseVersion(string value, string owner) =>
        Version.TryParse(value, out Version? version)
            ? version
            : throw new InvalidOperationException(
                $"The compile-time {owner} version '{value}' is invalid.");
}
