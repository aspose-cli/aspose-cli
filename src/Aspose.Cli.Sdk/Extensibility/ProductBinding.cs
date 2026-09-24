using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Ports;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>Invocation-scoped host services available during product activation.</summary>
public sealed class ProductActivationContext
{
    private readonly Dictionary<ProductDefinition, ProductBinding> _bindings = [];

    /// <summary>Resolved working directory.</summary>
    public required string WorkDirectory { get; init; }

    /// <summary>Resolved explicit license path selected for this invocation.</summary>
    public required string? LicensePath { get; init; }

    /// <summary>Explicit validated configuration changes for a license command result.</summary>
    public UserLicenseChanges? UserLicenseChanges { get; init; }

    /// <summary>Already validated process-lifetime licenses, when a long-lived host pins its SDK state.</summary>
    public Func<string, ILicenseGate>? RuntimeLicenseForProduct { get; init; }

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
    /// The one entry through which a command applies a font profile to this product's engine:
    /// the profile's directories, in addition to the system fonts, reach every layout, render
    /// and save of the engine until the returned scope is disposed, and the engine's previous
    /// fonts are restored then. A product without a font environment has nothing to scope.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The profile adds directories and the product has no font environment; callers accept
    /// <c>--font-dir</c> only for products that advertise explicit font profiles.
    /// </exception>
    public IDisposable UseFonts(FontSearchProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (FontEnvironment is { } fonts)
        {
            return fonts.UseFonts(profile);
        }
        return profile.IsAmbient
            ? NoFontScope.Instance
            : throw new InvalidOperationException(
                $"Product '{ProductId}' has no font environment for explicit font directories.");
    }

    private sealed class NoFontScope : IDisposable
    {
        public static readonly NoFontScope Instance = new();

        public void Dispose()
        {
        }
    }

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

    internal ProductBinding(
        string productId,
        Lazy<TPort> port,
        ILicenseGate licenseGate,
        Lazy<IFontEnvironment>? fontEnvironment)
        : base(productId, typeof(TPort), () => port.Value, licenseGate, fontEnvironment)
    {
        _port = port ?? throw new ArgumentNullException(nameof(port));
    }

    /// <summary>Product engine, constructed on first use.</summary>
    public TPort Port => _port.Value;

}

/// <summary>Activates one strongly typed product binding for an invocation.</summary>
public delegate ProductBinding<TPort> ProductActivator<TPort>(
    ProductActivationContext context)
    where TPort : class;
