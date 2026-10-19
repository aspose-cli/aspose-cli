using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Publication;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>Invocation-scoped host services available during product activation.</summary>
public sealed class ProductActivationContext
{
    private readonly Dictionary<ProductDefinition, ProductBinding> _bindings = [];

    /// <summary>Resolved working directory.</summary>
    public required string WorkDirectory { get; init; }

    /// <summary>Resolved explicit license path selected for this invocation.</summary>
    public required string? LicensePath { get; init; }

    /// <summary>
    /// True when the command asked for evaluation mode (<c>--license-mode evaluation</c>): no
    /// license source is read, not even a broken one.
    /// </summary>
    public bool EvaluationRequested { get; init; }

    /// <summary>Explicit validated configuration changes for a license command result.</summary>
    public UserLicenseChanges? UserLicenseChanges { get; init; }

    /// <summary>Per-user CLI configuration directory.</summary>
    public required string ConfigDirectory { get; init; }

    /// <summary>Environment lookup supplied explicitly for deterministic tests.</summary>
    public required Func<string, string?> EnvironmentVariable { get; init; }

    /// <summary>The invocation's atomic writer, through which the product's write pipeline publishes.</summary>
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
/// Product-neutral activation result. The product session remains type-safe and
/// is not exposed through this base class.
/// </summary>
public abstract class ProductBinding
{
    private readonly Func<object> _session;
    private readonly Lazy<IFontEnvironment> _fontEnvironment;

    internal ProductBinding(
        string productId,
        Type sessionType,
        Func<object> session,
        ILicenseGate licenseGate,
        Lazy<IFontEnvironment> fontEnvironment,
        OutputPipeline outputs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        ProductId = productId;
        SessionType = sessionType ?? throw new ArgumentNullException(nameof(sessionType));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        LicenseGate = licenseGate
            ?? throw new ArgumentNullException(nameof(licenseGate));
        _fontEnvironment = fontEnvironment ?? throw new ArgumentNullException(nameof(fontEnvironment));
        Publishing = outputs ?? throw new ArgumentNullException(nameof(outputs));
    }

    /// <summary>Stable product identifier.</summary>
    public string ProductId { get; }

    /// <summary>Exact product session type.</summary>
    public Type SessionType { get; }

    /// <summary>Product-specific license gate.</summary>
    public ILicenseGate LicenseGate { get; }

    /// <summary>
    /// The product's write pipeline, through which it publishes every output and which discloses
    /// evaluation output in the command's result.
    /// </summary>
    public OutputPipeline Publishing { get; }

    /// <summary>The product's font environment, created on first use.</summary>
    public IFontEnvironment FontEnvironment => _fontEnvironment.Value;

    internal object UntypedSession => _session();

    /// <summary>
    /// The one entry through which a command applies a font profile to this product's engine:
    /// the profile's directories, in addition to the system fonts, reach every layout, render
    /// and save of the engine until the returned scope is disposed, and the engine's previous
    /// fonts are restored then.
    /// </summary>
    public IDisposable UseFonts(FontSearchProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return FontEnvironment.UseFonts(profile);
    }

    /// <summary>
    /// Creates a licensed-engine binding whose product publishes its outputs through the SDK
    /// write pipeline, which recognizes evaluation marks through <paramref name="evaluation"/>.
    /// The product session and the font environment are deferred independently;
    /// <paramref name="applyLicense"/> applies the resolved license to the product's SDK once.
    /// </summary>
    public static ProductBinding<TSession> Create<TSession, TDocument>(
        ProductActivationContext context,
        string productId,
        Action<Stream> applyLicense,
        IEvaluationProfile<TDocument> evaluation,
        Func<OutputPipeline<TDocument>, TSession> createSession,
        Func<ILicenseState, IFontEnvironment> createFontEnvironment)
        where TSession : class
        where TDocument : class
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentNullException.ThrowIfNull(createSession);
        ArgumentNullException.ThrowIfNull(createFontEnvironment);
        ILicenseGate license = ProductLicenseGateFactory.Create(
            context,
            productId,
            applyLicense);
        var outputs = new OutputPipeline<TDocument>(license, evaluation, context.SafeFileWriter);
        return new ProductBinding<TSession>(
            productId,
            new Lazy<TSession>(() => createSession(outputs)),
            license,
            new Lazy<IFontEnvironment>(() => createFontEnvironment(outputs)),
            outputs);
    }
}

/// <summary>Strongly typed product binding created once per invocation.</summary>
/// <typeparam name="TSession">The product's engine session.</typeparam>
public sealed class ProductBinding<TSession> : ProductBinding
    where TSession : class
{
    private readonly Lazy<TSession> _session;

    internal ProductBinding(
        string productId,
        Lazy<TSession> session,
        ILicenseGate licenseGate,
        Lazy<IFontEnvironment> fontEnvironment,
        OutputPipeline outputs)
        : base(productId, typeof(TSession), () => session.Value, licenseGate, fontEnvironment, outputs)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    /// <summary>The product's engine session, created on first use.</summary>
    public TSession Session => _session.Value;
}

/// <summary>Activates one strongly typed product binding for an invocation.</summary>
public delegate ProductBinding<TSession> ProductActivator<TSession>(
    ProductActivationContext context)
    where TSession : class;
