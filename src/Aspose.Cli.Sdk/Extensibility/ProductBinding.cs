using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Ports;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>Invocation-scoped host services available during product activation.</summary>
public sealed class ProductActivationContext
{
    private readonly Dictionary<ProductDefinition, ProductBinding> _bindings = [];
    private readonly Dictionary<object, object> _capabilities =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>Resolved working directory.</summary>
    public required string WorkDirectory { get; init; }

    /// <summary>Resolves the explicit license path selected for a product.</summary>
    public required Func<string, string?> LicensePathForProduct { get; init; }

    /// <summary>Per-user CLI configuration directory.</summary>
    public required string ConfigDirectory { get; init; }

    /// <summary>Environment lookup supplied explicitly for deterministic tests.</summary>
    public required Func<string, string?> EnvironmentVariable { get; init; }

    /// <summary>Safe atomic writer shared by mutating product workflows.</summary>
    public required SafeFileWriter SafeFileWriter { get; init; }

    /// <summary>Invocation-scoped resource ledger shared by every product phase.</summary>
    public required ResourceBudgetLedger ResourceBudgets { get; init; }

    internal object SyncRoot { get; } = new();

    internal bool TryGetBinding(
        ProductDefinition product,
        out ProductBinding? binding) =>
        _bindings.TryGetValue(product, out binding);

    internal void AddBinding(
        ProductDefinition product,
        ProductBinding binding) =>
        _bindings.Add(product, binding);

    internal bool TryGetCapability(object slot, out object? capability) =>
        _capabilities.TryGetValue(slot, out capability);

    internal void AddCapability(object slot, object capability) =>
        _capabilities.Add(slot, capability);
}

/// <summary>Creates a resolved product license gate with uniform failure behavior.</summary>
internal static class ProductLicenseGateFactory
{
    public static ILicenseGate Create(
        ProductActivationContext context,
        string productId,
        Func<LicenseResolution, ILicenseGate> factory)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        ArgumentNullException.ThrowIfNull(factory);
        try
        {
            LicenseResolution resolution = LicenseResolver.Resolve(
                context.LicensePathForProduct(productId),
                productId,
                context.EnvironmentVariable,
                context.WorkDirectory,
                context.ConfigDirectory);
            return factory(resolution)
                ?? throw new InvalidOperationException(
                    $"Product '{productId}' returned no license gate.");
        }
        catch (Errors.CliException exception)
        {
            return new UnavailableLicenseGate(exception);
        }
    }

    private sealed class UnavailableLicenseGate(
        Errors.CliException exception) : ILicenseGate
    {
        public bool IsApplicable => true;

        public LicenseResolution Resolution => LicenseResolution.None;

        public LicenseState EnsureApplied() => throw exception;
    }
}

/// <summary>A license gate for engines to which Aspose licensing does not apply.</summary>
internal sealed class LicenseNotApplicableGate : ILicenseGate
{
    public static LicenseNotApplicableGate Instance { get; } = new();

    private LicenseNotApplicableGate()
    {
    }

    public bool IsApplicable => false;

    public LicenseResolution Resolution => LicenseResolution.None;

    public LicenseState EnsureApplied() => LicenseState.NotApplicable;
}

/// <summary>
/// Product-neutral activation result. The product port remains type-safe and
/// is not exposed through this base class.
/// </summary>
public abstract class ProductBinding
{
    private readonly Func<object> _port;

    internal ProductBinding(
        string productId,
        Type portType,
        Func<object> port,
        ILicenseGate licenseGate,
        Lazy<IFontEnvironment>? fontEnvironment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        ProductId = productId;
        PortType = portType ?? throw new ArgumentNullException(nameof(portType));
        _port = port ?? throw new ArgumentNullException(nameof(port));
        LicenseGate = licenseGate
            ?? throw new ArgumentNullException(nameof(licenseGate));
        FontEnvironmentFactory = fontEnvironment;
    }

    /// <summary>Stable product identifier.</summary>
    public string ProductId { get; }

    /// <summary>Exact product port type.</summary>
    public Type PortType { get; }

    /// <summary>Product-specific license gate.</summary>
    public ILicenseGate LicenseGate { get; }

    /// <summary>Product-specific font environment, when the engine exposes one.</summary>
    public IFontEnvironment? FontEnvironment => FontEnvironmentFactory?.Value;

    /// <summary>Whether the product exposes font diagnostics without activating them.</summary>
    public bool HasFontEnvironment => FontEnvironmentFactory is not null;

    internal Lazy<IFontEnvironment>? FontEnvironmentFactory { get; }

    internal object UntypedPort => _port();

    /// <summary>
    /// Creates a binding whose concrete port implementation also supplies the
    /// font environment while the public port remains interface-typed.
    /// </summary>
    public static ProductBinding<TPort> Create<TPort, TImplementation>(
        ProductActivationContext context,
        string productId,
        Func<LicenseResolution, ILicenseGate> createLicenseGate,
        Func<ILicenseGate, TImplementation> createPort)
        where TPort : class
        where TImplementation : class, TPort, IFontEnvironment
    {
        ArgumentNullException.ThrowIfNull(createPort);
        ILicenseGate license = ProductLicenseGateFactory.Create(
            context,
            productId,
            createLicenseGate);
        var implementation = new Lazy<TImplementation>(
            () => createPort(license));
        return new ProductBinding<TPort>(
            productId,
            new Lazy<TPort>(() => implementation.Value),
            license,
            new Lazy<IFontEnvironment>(() => implementation.Value));
    }

    /// <summary>
    /// Creates a binding with independently deferred product and font ports.
    /// </summary>
    public static ProductBinding<TPort> Create<TPort>(
        ProductActivationContext context,
        string productId,
        Func<LicenseResolution, ILicenseGate> createLicenseGate,
        Func<ILicenseGate, TPort> createPort,
        Func<ILicenseGate, IFontEnvironment> createFontEnvironment)
        where TPort : class
    {
        ArgumentNullException.ThrowIfNull(createPort);
        ArgumentNullException.ThrowIfNull(createFontEnvironment);
        ILicenseGate license = ProductLicenseGateFactory.Create(
            context,
            productId,
            createLicenseGate);
        return new ProductBinding<TPort>(
            productId,
            new Lazy<TPort>(() => createPort(license)),
            license,
            new Lazy<IFontEnvironment>(() => createFontEnvironment(license)));
    }

    /// <summary>Creates a licensed-engine binding without font diagnostics.</summary>
    public static ProductBinding<TPort> Create<TPort>(
        ProductActivationContext context,
        string productId,
        Func<LicenseResolution, ILicenseGate> createLicenseGate,
        Func<ILicenseGate, TPort> createPort)
        where TPort : class
    {
        ArgumentNullException.ThrowIfNull(createPort);
        ILicenseGate license = ProductLicenseGateFactory.Create(
            context,
            productId,
            createLicenseGate);
        return new ProductBinding<TPort>(
            productId,
            new Lazy<TPort>(() => createPort(license)),
            license,
            fontEnvironment: null);
    }

    /// <summary>
    /// Creates a binding for an engine to which Aspose licensing does not apply.
    /// No activation context is accepted, so license resolution cannot occur.
    /// </summary>
    public static ProductBinding<TPort> CreateLicenseFree<TPort>(
        string productId,
        Func<ILicenseGate, TPort> createPort)
        where TPort : class
    {
        ArgumentNullException.ThrowIfNull(createPort);
        ILicenseGate license = LicenseNotApplicableGate.Instance;
        return new ProductBinding<TPort>(
            productId,
            new Lazy<TPort>(() => createPort(license)),
            license,
            fontEnvironment: null);
    }

    /// <summary>Creates a license-free binding whose port also supplies font diagnostics.</summary>
    public static ProductBinding<TPort> CreateLicenseFree<TPort, TImplementation>(
        string productId,
        Func<ILicenseGate, TImplementation> createPort)
        where TPort : class
        where TImplementation : class, TPort, IFontEnvironment
    {
        ArgumentNullException.ThrowIfNull(createPort);
        ILicenseGate license = LicenseNotApplicableGate.Instance;
        var implementation = new Lazy<TImplementation>(() => createPort(license));
        return new ProductBinding<TPort>(
            productId,
            new Lazy<TPort>(() => implementation.Value),
            license,
            new Lazy<IFontEnvironment>(() => implementation.Value));
    }
}

/// <summary>Strongly typed product binding created once per invocation.</summary>
/// <typeparam name="TPort">Product-specific typed port.</typeparam>
public sealed class ProductBinding<TPort> : ProductBinding
    where TPort : class
{
    private readonly Lazy<TPort> _port;
    private readonly IReadOnlyDictionary<object, object> _capabilities;

    internal ProductBinding(
        string productId,
        Lazy<TPort> port,
        ILicenseGate licenseGate,
        Lazy<IFontEnvironment>? fontEnvironment,
        IReadOnlyDictionary<object, object>? capabilities = null)
        : base(productId, typeof(TPort), () => port.Value, licenseGate, fontEnvironment)
    {
        _port = port ?? throw new ArgumentNullException(nameof(port));
        _capabilities = capabilities
            ?? new Dictionary<object, object>(ReferenceEqualityComparer.Instance);
    }

    /// <summary>Product engine, constructed on first use.</summary>
    public TPort Port => _port.Value;

    /// <summary>Returns a predeclared optional capability when its provider is compiled.</summary>
    public TCapability? Optional<TCapability>(
        ProductCapability<TCapability> slot)
        where TCapability : class
    {
        ArgumentNullException.ThrowIfNull(slot);
        return _capabilities.TryGetValue(slot, out object? capability)
            ? (TCapability)capability
            : null;
    }

    internal ProductBinding<TPort> WithCapabilities(
        IReadOnlyDictionary<object, object> capabilities) =>
        new(
            ProductId,
            _port,
            LicenseGate,
            FontEnvironmentFactory,
            capabilities);
}

/// <summary>Activates one strongly typed product binding for an invocation.</summary>
public delegate ProductBinding<TPort> ProductActivator<TPort>(
    ProductActivationContext context)
    where TPort : class;
