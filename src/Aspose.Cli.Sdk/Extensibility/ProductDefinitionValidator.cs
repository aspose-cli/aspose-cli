using System.CommandLine;
using Aspose.Cli.Sdk.Contracts;

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
        ValidateBudgetOptions(definition);
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

    private static void ValidateOperationSchemas(
        ProductDefinition definition,
        IReadOnlyList<string> schemaIds)
    {
        string productId = definition.Manifest.Id;
        string prefix = $"v2/{productId}/";
        foreach (ProductOperationCommand command in
            definition.Manifest.Operations)
        {
            ProductOperationDescriptor operation = command.Descriptor;
            if (!operation.InputSchema.StartsWith(
                    prefix,
                    StringComparison.Ordinal)
                || (command.GeneratedSchema is null
                    && !schemaIds.Contains(
                        operation.InputSchema,
                        StringComparer.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Product '{productId}' operation command '{operation.Command}' references unowned schema '{operation.InputSchema}'.");
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
        var commands = new HashSet<string>(StringComparer.Ordinal);
        foreach (ProductOperationDescriptor? operation in manifest.Operations.Select(static command => command?.Descriptor))
        {
            string[] command = operation?.Command.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
            if (operation is null
                || command.Length == 0
                || command.Any(static segment => !IsToken(segment))
                || !string.Equals(operation.Command, string.Join(' ', command), StringComparison.Ordinal)
                || !commands.Add(operation.Command))
            {
                throw new InvalidOperationException(
                    $"Product '{manifest.Id}' declares an invalid or duplicate operation command.");
            }
            if (string.IsNullOrWhiteSpace(operation.InputSchema)
                || operation.MaximumOperations < 1
                || operation.Ops.Count == 0
                || operation.Ops.Any(static op => !IsToken(op))
                || operation.Ops.Distinct(StringComparer.Ordinal).Count() != operation.Ops.Count)
            {
                throw new InvalidOperationException(
                    $"Product '{manifest.Id}' operation command '{operation.Command}' has an invalid schema, limit or operation list.");
            }
        }
    }

    /// <summary>
    /// A budget that names a command option is raised through that option, so the option
    /// must exist: the host otherwise enforces the budget's maximum instead of its default.
    /// </summary>
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
        CollectOptions(definition.CreateCommand(InertCommandHostFactory.Instance), registered);
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

    /// <summary>Builds a command tree for inspection; its commands cannot run.</summary>
    private sealed class InertCommandHostFactory : IProductCommandHostFactory
    {
        public static InertCommandHostFactory Instance { get; } = new();

        public IProductCommandHost<TPort> Create<TPort>(string productId)
            where TPort : class => new InertCommandHost<TPort>();
    }

    private sealed class InertCommandHost<TPort> : IProductCommandHost<TPort>
        where TPort : class
    {
        public int Run(
            ParseResult parseResult,
            Func<ProductCommandContext<TPort>, ResultEnvelope> handler) =>
            throw new InvalidOperationException("A command tree built for validation cannot run.");
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
