namespace Aspose.Cli.Sdk.Extensibility;

using System.CommandLine;
using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Serialization;

/// <summary>
/// Immutable, type-erased product definition consumed by the product catalog.
/// Strong typing is retained while building the definition and at the bound
/// execution edge; the host does not resolve engines by string or service type.
/// </summary>
public sealed class ProductDefinition
{
    internal ProductDefinition(
        ProductManifest manifest,
        Type portType,
        FileRouteDefinition files,
        IReadOnlyList<FormatDescriptor> formats,
        ProductJsonDefinition json,
        ProductPreviewDefinition preview,
        ProductViewDefinition view,
        IReadOnlyList<ProductOutputDefinition> outputs,
        IReadOnlyList<DiagnosticDescriptor> diagnostics,
        Func<object, IReadOnlyList<Aspose.Cli.Sdk.Contracts.DoctorCheck>>? doctorChecks,
        Func<IProductCommandHostFactory, Command> commandFactory,
        Func<
            ProductActivationContext,
            ProductBinding> bindingFactory)
    {
        Manifest = manifest;
        PortType = portType;
        Files = files;
        Formats = formats;
        Json = json ?? throw new ArgumentNullException(nameof(json));
        Preview = preview ?? throw new ArgumentNullException(nameof(preview));
        View = view ?? throw new ArgumentNullException(nameof(view));
        Outputs = outputs;
        Diagnostics = diagnostics;
        DoctorChecks = doctorChecks;
        CommandFactory = commandFactory
            ?? throw new ArgumentNullException(nameof(commandFactory));
        BindingFactory = bindingFactory
            ?? throw new ArgumentNullException(nameof(bindingFactory));
    }

    /// <summary>Product identity and advertised public surface.</summary>
    public ProductManifest Manifest { get; }

    /// <summary>Strongly typed port bound by this product.</summary>
    public Type PortType { get; }

    /// <summary>File ownership and explicit-input associations.</summary>
    public FileRouteDefinition Files { get; }

    /// <summary>Canonical product format declarations.</summary>
    public IReadOnlyList<FormatDescriptor> Formats { get; }

    /// <summary>Product-owned source-generated JSON metadata.</summary>
    public ProductJsonDefinition Json { get; }

    /// <summary>Product-owned preview semantics and browser presentation.</summary>
    public ProductPreviewDefinition Preview { get; }

    /// <summary>Product-owned views shared by static review and live display.</summary>
    public ProductViewDefinition View { get; }

    /// <summary>Human-readable result renderers owned by this product.</summary>
    public IReadOnlyList<ProductOutputDefinition> Outputs { get; }

    /// <summary>Immutable error and warning descriptors owned by this product.</summary>
    public IReadOnlyList<DiagnosticDescriptor> Diagnostics { get; }

    /// <summary>Creates the product-owned command tree through a typed host.</summary>
    public Command CreateCommand(IProductCommandHostFactory hostFactory)
    {
        ArgumentNullException.ThrowIfNull(hostFactory);
        return CommandFactory(hostFactory)
            ?? throw new InvalidOperationException(
                $"Product '{Manifest.Id}' returned no command contribution.");
    }

    internal ProductBinding Activate(
        ProductActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ProductBinding binding = BindingFactory(context)
            ?? throw new InvalidOperationException(
                $"Product '{Manifest.Id}' returned no binding.");
        if (!string.Equals(
                binding.ProductId,
                Manifest.Id,
                StringComparison.Ordinal)
            || binding.PortType != PortType)
        {
            throw new InvalidOperationException(
                $"Product '{Manifest.Id}' activated an incompatible binding.");
        }
        if (binding.LicenseGate.IsApplicable != Manifest.Engine.LicenseApplicable)
        {
            throw new InvalidOperationException(
                $"Product '{Manifest.Id}' license binding does not match its manifest.");
        }
        if (binding.HasFontEnvironment != Manifest.Engine.SupportsFontDiagnostics)
        {
            throw new InvalidOperationException(
                $"Product '{Manifest.Id}' font binding does not match its manifest.");
        }
        return binding;
    }

    /// <summary>
    /// Evaluates product-owned diagnostics against an activated binding.
    /// </summary>
    public IReadOnlyList<Aspose.Cli.Sdk.Contracts.DoctorCheck> GetDoctorChecks(
        ProductBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (binding.ProductId != Manifest.Id || binding.PortType != PortType)
        {
            throw new InvalidOperationException(
                $"Binding '{binding.ProductId}' does not belong to product '{Manifest.Id}'.");
        }
        return DoctorChecks?.Invoke(binding.UntypedPort) ?? [];
    }

    private Func<IProductCommandHostFactory, Command> CommandFactory { get; }

    private Func<object, IReadOnlyList<Aspose.Cli.Sdk.Contracts.DoctorCheck>>?
        DoctorChecks
    { get; }

    private Func<
        ProductActivationContext,
        ProductBinding> BindingFactory
    { get; }
}

/// <summary>Starts a strongly typed, pure product definition.</summary>
public static class Product
{
    /// <summary>Creates a definition builder for one typed product port.</summary>
    public static ProductDefinitionBuilder<TPort> Define<TPort>(ProductManifest manifest)
        where TPort : class =>
        new(manifest);
}

/// <summary>Builds an immutable definition without access to the product catalog.</summary>
/// <typeparam name="TPort">Product-specific typed port.</typeparam>
public sealed class ProductDefinitionBuilder<TPort>
    where TPort : class
{
    private readonly ProductManifest _manifest;
    private readonly List<FormatDescriptor> _formats = [];
    private ProductJsonDefinition? _json;
    private ProductPreviewDefinition? _preview;
    private ProductViewDefinition? _view;
    private readonly List<ProductOutputDefinition> _outputs = [];
    private readonly List<DiagnosticDescriptor> _diagnostics = [];
    private Func<object, IReadOnlyList<Aspose.Cli.Sdk.Contracts.DoctorCheck>>? _doctorChecks;
    private Func<IProductCommandHostFactory, Command>? _commandFactory;
    private Func<
        ProductActivationContext,
        ProductBinding>? _bindingFactory;
    private bool _formatsDeclared;
    private bool _diagnosticsDeclared;
    private bool _built;

    internal ProductDefinitionBuilder(ProductManifest manifest)
    {
        _manifest = Snapshot(
            manifest ?? throw new ArgumentNullException(nameof(manifest)));
    }

    /// <summary>Registers canonical input, output, and routing format metadata.</summary>
    public ProductDefinitionBuilder<TPort> Formats(
        IEnumerable<FormatDescriptor> formats)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(formats);
        _formats.AddRange(formats);
        _formatsDeclared = true;
        return this;
    }

    /// <summary>Registers the deferred typed product activator.</summary>
    public ProductDefinitionBuilder<TPort> Activator(
        ProductActivator<TPort> activator)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(activator);
        _bindingFactory = context =>
        {
            ProductBinding<TPort> binding = activator(context)
                ?? throw new InvalidOperationException(
                    $"Product '{_manifest.Id}' returned no binding.");
            return binding;
        };
        return this;
    }

    /// <summary>Registers the product-owned System.CommandLine command tree.</summary>
    public ProductDefinitionBuilder<TPort> Commands(
        Func<IProductCommandHost<TPort>, Command> factory)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(factory);
        _commandFactory = hostFactory =>
            factory(hostFactory.Create<TPort>(_manifest.Id));
        return this;
    }

    /// <summary>Registers product-owned source-generated JSON metadata.</summary>
    public ProductDefinitionBuilder<TPort> Json(ProductJsonDefinition json)
    {
        EnsureMutable();
        _json = json ?? throw new ArgumentNullException(nameof(json));
        return this;
    }

    /// <summary>Registers the product-owned typed preview adapter.</summary>
    public ProductDefinitionBuilder<TPort> Preview(
        IProductPreviewAdapter<TPort> adapter)
    {
        EnsureMutable();
        _preview = ProductPreviewDefinition.Create(adapter, _manifest.Id);
        return this;
    }

    /// <summary>Registers the product-owned view adapter used by review and live display.</summary>
    public ProductDefinitionBuilder<TPort> View(
        IProductViewAdapter<TPort> adapter)
    {
        EnsureMutable();
        _view = ProductViewDefinition.Create(adapter, _manifest.Id);
        return this;
    }

    /// <summary>Registers a human-readable renderer for one result type.</summary>
    public ProductDefinitionBuilder<TPort> Output<TResult>(
        Action<TResult, Output.TableSurface> renderer)
        where TResult : Aspose.Cli.Sdk.Contracts.ResultEnvelope
    {
        EnsureMutable();
        _outputs.Add(ProductOutputDefinition.Create(renderer));
        return this;
    }

    /// <summary>Registers immutable diagnostic descriptors owned by this product.</summary>
    public ProductDefinitionBuilder<TPort> Diagnostics(
        IEnumerable<DiagnosticDescriptor> diagnostics)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(diagnostics);
        _diagnostics.AddRange(diagnostics);
        _diagnosticsDeclared = true;
        return this;
    }

    /// <summary>Registers product-owned diagnostics against the typed product port.</summary>
    public ProductDefinitionBuilder<TPort> Doctor(
        Func<TPort, IReadOnlyList<Aspose.Cli.Sdk.Contracts.DoctorCheck>> checks)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(checks);
        _doctorChecks = port => checks((TPort)port);
        return this;
    }

    /// <summary>Freezes and returns the immutable product definition.</summary>
    public ProductDefinition Build()
    {
        EnsureMutable();
        if (!_formatsDeclared)
        {
            throw Missing(nameof(Formats));
        }
        if (_json is null)
        {
            throw Missing(nameof(Json));
        }
        if (_preview is null)
        {
            throw Missing(nameof(Preview));
        }
        if (_view is null)
        {
            throw Missing(nameof(View));
        }
        if (_outputs.Count == 0)
        {
            throw Missing(nameof(Output));
        }
        if (!_diagnosticsDeclared)
        {
            throw Missing(nameof(Diagnostics));
        }
        _ = _commandFactory ?? throw Missing(nameof(Commands));
        _ = _bindingFactory ?? throw Missing(nameof(Activator));
        _built = true;
        IReadOnlyList<FormatDescriptor> formats = Array.AsReadOnly(
            _formats.Select(Snapshot).ToArray());
        FileRouteDefinition files = CreateFileRoutes(formats);
        return new ProductDefinition(
            _manifest,
            typeof(TPort),
            files,
            formats,
            _json,
            _preview,
            _view,
            Array.AsReadOnly(_outputs.ToArray()),
            Array.AsReadOnly(_diagnostics.ToArray()),
            _doctorChecks,
            _commandFactory,
            _bindingFactory);
    }

    private static FileRouteDefinition CreateFileRoutes(
        IReadOnlyList<FormatDescriptor> formats)
    {
        IFileRecognizer[] recognizers = formats
            .Select(static format => format.Recognizer)
            .Where(static recognizer => recognizer is not null)
            .Cast<IFileRecognizer>()
            .ToArray();
        if (recognizers.Skip(1).Any(recognizer =>
                !ReferenceEquals(recognizer, recognizers[0])))
        {
            throw new InvalidOperationException(
                "A product format set can declare at most one shared file recognizer.");
        }

        FormatDescriptor[] defaultFormats = formats
            .Where(static format =>
                format.Ownership == RouteOwnership.Default
                && format.Uses.HasFlag(FormatUse.Input))
            .ToArray();
        bool hasDeclarativeRules = defaultFormats.Any(
            static format => format.Recognition is not null);
        if (recognizers.Length > 0 && hasDeclarativeRules)
        {
            throw new InvalidOperationException(
                "A product format set cannot mix a custom file recognizer with declarative recognition rules.");
        }
        if (recognizers.Length == 0
            && defaultFormats.Any(static format =>
                format.Recognition is null))
        {
            string missing = string.Join(
                ", ",
                defaultFormats
                    .Where(static format => format.Recognition is null)
                    .Select(static format => format.Id));
            throw new InvalidOperationException(
                $"Default input formats require bounded recognition rules: {missing}.");
        }

        string[] defaultOwners = formats
            .Where(static format =>
                format.Ownership == RouteOwnership.Default
                && format.Uses.HasFlag(FormatUse.Input))
            .SelectMany(static format => format.Extensions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new FileRouteDefinition
        {
            DefaultOwnerExtensions = defaultOwners,
            AcceptedInputExtensions = formats
                .Where(static format => format.Uses.HasFlag(FormatUse.Input))
                .SelectMany(static format => format.Extensions)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Recognizer = recognizers.FirstOrDefault()
                ?? (defaultOwners.Length == 0
                    ? null
                    : new DeclarativeFormatRecognizer(formats)),
        };
    }

    private static ProductManifest Snapshot(ProductManifest manifest) =>
        manifest with
        {
            Operations = Array.AsReadOnly(
                manifest.Operations.Select(static operation =>
                    operation with { }).ToArray()),
            Engine = manifest.Engine with { },
            AvailableEngines = ReadOnly(manifest.AvailableEngines),
            ResourceBudgets = Array.AsReadOnly(
                manifest.ResourceBudgets.Select(static budget =>
                    budget with { }).ToArray()),
        };

    private static FormatDescriptor Snapshot(FormatDescriptor format) =>
        format with
        {
            Extensions = ReadOnly(format.Extensions),
            Aliases = ReadOnly(format.Aliases),
            Operations = ReadOnly(format.Operations),
            Recognition = format.Recognition?.Snapshot(),
        };

    private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }

    private void EnsureMutable()
    {
        if (_built)
        {
            throw new InvalidOperationException("The product definition has already been built.");
        }
    }

    private InvalidOperationException Missing(string member) =>
        new($"Product '{_manifest.Id}' must declare {member} before Build().");
}
