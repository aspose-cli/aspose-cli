namespace Aspose.Cli.Sdk.Extensibility;

using System.CommandLine;
using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Operations;
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
        Type sessionType,
        FileRouteDefinition files,
        IReadOnlyList<FormatDescriptor> formats,
        ProductJsonDefinition json,
        ProductViewDefinition view,
        Func<IReadOnlyList<ProductOutputDefinition>> outputs,
        Func<IReadOnlyList<ProductOperationCommand>> operations,
        IReadOnlyList<DiagnosticDescriptor> diagnostics,
        Func<ProductCommandRunner, Command> commandFactory,
        Func<
            ProductActivationContext,
            ProductBinding> bindingFactory)
    {
        Manifest = manifest;
        SessionType = sessionType;
        Files = files;
        Formats = formats;
        Json = json ?? throw new ArgumentNullException(nameof(json));
        View = view ?? throw new ArgumentNullException(nameof(view));
        _outputs = new Lazy<IReadOnlyList<ProductOutputDefinition>>(
            outputs ?? throw new ArgumentNullException(nameof(outputs)));
        _operations = new Lazy<IReadOnlyList<ProductOperationCommand>>(
            operations ?? throw new ArgumentNullException(nameof(operations)));
        Diagnostics = diagnostics;
        CommandFactory = commandFactory
            ?? throw new ArgumentNullException(nameof(commandFactory));
        BindingFactory = bindingFactory
            ?? throw new ArgumentNullException(nameof(bindingFactory));
    }

    /// <summary>Product identity and advertised public surface.</summary>
    public ProductManifest Manifest { get; }

    /// <summary>The engine session type this product binds.</summary>
    public Type SessionType { get; }

    /// <summary>File ownership and explicit-input associations.</summary>
    public FileRouteDefinition Files { get; }

    /// <summary>Canonical product format declarations.</summary>
    public IReadOnlyList<FormatDescriptor> Formats { get; }

    /// <summary>Product-owned source-generated JSON metadata.</summary>
    public ProductJsonDefinition Json { get; }

    /// <summary>Product-owned views shared by static review and live display.</summary>
    public ProductViewDefinition View { get; }

    /// <summary>
    /// Human-readable result renderers owned by this product, one per result type; a menu's
    /// renderers come from its command definitions, created on first access.
    /// </summary>
    public IReadOnlyList<ProductOutputDefinition> Outputs => _outputs.Value;

    private readonly Lazy<IReadOnlyList<ProductOutputDefinition>> _outputs;

    /// <summary>
    /// The commands that apply this product's operation documents, in menu order; each comes
    /// from its edit definition, created on first access.
    /// </summary>
    public IReadOnlyList<ProductOperationCommand> Operations => _operations.Value;

    private readonly Lazy<IReadOnlyList<ProductOperationCommand>> _operations;

    /// <summary>Immutable error and warning descriptors owned by this product.</summary>
    public IReadOnlyList<DiagnosticDescriptor> Diagnostics { get; }

    /// <summary>Creates the product's command tree, whose commands run through <paramref name="runner"/>.</summary>
    /// <param name="runner">The host pipeline, which runs each command in a scope for this product.</param>
    public Command CreateCommand(ProductCommandRunner runner)
    {
        ArgumentNullException.ThrowIfNull(runner);
        return CommandFactory(runner)
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
                StringComparison.Ordinal))
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

    private Func<ProductCommandRunner, Command> CommandFactory { get; }

    private Func<
        ProductActivationContext,
        ProductBinding> BindingFactory
    { get; }
}

/// <summary>Starts a strongly typed, pure product definition.</summary>
public static class Product
{
    /// <summary>Creates a definition builder for one product and its engine session type.</summary>
    public static ProductDefinitionBuilder<TSession> Define<TSession>(ProductManifest manifest)
        where TSession : class =>
        new(manifest);
}

/// <summary>Builds an immutable definition without access to the product catalog.</summary>
/// <remarks>
/// A product lists its commands as a menu: each <c>Command</c> line pairs a command definition
/// with the static handler that serves it, and <c>Group</c> nests lines under a group command.
/// The menu lines live in <c>Aspose.Cli.Sdk.Extensibility.Commanding.ProductMenu</c>; their
/// order is the order of help and capabilities.
/// </remarks>
/// <typeparam name="TSession">The product's engine session.</typeparam>
public sealed class ProductDefinitionBuilder<TSession>
    where TSession : class
{
    private readonly ProductManifest _manifest;
    private readonly List<FormatDescriptor> _formats = [];
    private ProductJsonDefinition? _json;
    private IProductViewAdapter<TSession>? _viewAdapter;
    private readonly List<DiagnosticDescriptor> _diagnostics = [];
    private readonly List<IMenuEntry<TSession>> _menu = [];
    private string? _description;
    private Func<CommandHelp>? _help;
    private ProductGuard<TSession>? _guard;
    private Func<string, string?>? _detectFormat;
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
    public ProductDefinitionBuilder<TSession> Formats(
        IEnumerable<FormatDescriptor> formats)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(formats);
        _formats.AddRange(formats);
        _formatsDeclared = true;
        return this;
    }

    /// <summary>Registers the deferred typed product activator.</summary>
    public ProductDefinitionBuilder<TSession> Activator(
        ProductActivator<TSession> activator)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(activator);
        _bindingFactory = context =>
        {
            ProductBinding<TSession> binding = activator(context)
                ?? throw new InvalidOperationException(
                    $"Product '{_manifest.Id}' returned no binding.");
            return binding;
        };
        return this;
    }

    /// <summary>Registers product-owned source-generated JSON metadata.</summary>
    public ProductDefinitionBuilder<TSession> Json(ProductJsonDefinition json)
    {
        EnsureMutable();
        _json = json ?? throw new ArgumentNullException(nameof(json));
        return this;
    }

    /// <summary>Registers the product-owned view adapter used by review and live display.</summary>
    public ProductDefinitionBuilder<TSession> View(
        IProductViewAdapter<TSession> adapter)
    {
        EnsureMutable();
        _viewAdapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        return this;
    }

    /// <summary>Describes the product command of a menu: its help and, optionally, its examples and links.</summary>
    /// <param name="description">The product command help.</param>
    /// <param name="help">Creates the examples and links; called each time the command tree is built.</param>
    public ProductDefinitionBuilder<TSession> Describe(string description, Func<CommandHelp>? help = null)
    {
        EnsureMutable();
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        _description = description;
        _help = help;
        return this;
    }

    /// <summary>
    /// Runs <paramref name="guard"/> around every handler call of the product's menu commands,
    /// inside their font scope, and around every call of its view adapter.
    /// </summary>
    public ProductDefinitionBuilder<TSession> Guard(ProductGuard<TSession> guard)
    {
        EnsureMutable();
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        return this;
    }

    /// <summary>
    /// Registers how the product detects a file's format id from its content without loading it
    /// (null when it cannot tell). Only the menu commands that edit their input use it, to choose
    /// among the formats an output extension names, as <c>EncryptPassword()</c> does for an edit
    /// that keeps its input's format; a command that creates or converts a file never calls it.
    /// </summary>
    public ProductDefinitionBuilder<TSession> DetectFormat(Func<string, string?> detect)
    {
        EnsureMutable();
        _detectFormat = detect ?? throw new ArgumentNullException(nameof(detect));
        return this;
    }

    /// <summary>Adds one menu line, in help order.</summary>
    internal ProductDefinitionBuilder<TSession> AddMenuEntry(IMenuEntry<TSession> entry)
    {
        EnsureMutable();
        _menu.Add(entry ?? throw new ArgumentNullException(nameof(entry)));
        return this;
    }

    /// <summary>
    /// Registers immutable diagnostic descriptors owned by this product; one declared without an
    /// owner takes the product id.
    /// </summary>
    public ProductDefinitionBuilder<TSession> Diagnostics(
        IEnumerable<DiagnosticDescriptor> diagnostics)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(diagnostics);
        _diagnostics.AddRange(diagnostics.Select(descriptor =>
            descriptor is { Owner.Length: 0 } ? descriptor with { Owner = _manifest.Id } : descriptor));
        _diagnosticsDeclared = true;
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
        if (_viewAdapter is null)
        {
            throw Missing(nameof(View));
        }
        if (!_diagnosticsDeclared)
        {
            throw Missing(nameof(Diagnostics));
        }
        if (_description is null)
        {
            throw Missing(nameof(Describe));
        }
        if (_menu.Count == 0)
        {
            throw Missing("Command");
        }
        ProductGuard<TSession>? guard = _guard;
        ProductViewDefinition view = ProductViewDefinition.Create(
            _viewAdapter,
            _manifest.Id,
            guard is null ? null : (session, run) => guard((TSession)session, run));
        _ = _bindingFactory ?? throw Missing(nameof(Activator));
        _built = true;
        IReadOnlyList<FormatDescriptor> formats = Array.AsReadOnly(
            _formats.Select(Snapshot).ToArray());
        FileRouteDefinition files = CreateFileRoutes(formats);
        return new ProductDefinition(
            _manifest,
            typeof(TSession),
            files,
            formats,
            _json,
            view,
            Outputs,
            Operations,
            Array.AsReadOnly(_diagnostics.ToArray()),
            CreateCommand,
            _bindingFactory);
    }

    private Command CreateCommand(ProductCommandRunner runner)
    {
        var context = new MenuContext<TSession>(runner, _guard, _detectFormat);
        var product = new Command(_manifest.Id, _description);
        foreach (IMenuEntry<TSession> entry in _menu)
        {
            product.Subcommands.Add(entry.Create(context));
        }

        return _help?.Invoke() is { } help ? product.WithExamples(help.Examples, help.Links) : product;
    }

    // One renderer per result type: commands that share a result type share its renderer method.
    private IReadOnlyList<ProductOutputDefinition> Outputs()
    {
        var byType = new Dictionary<Type, ProductOutputDefinition>();
        var ordered = new List<ProductOutputDefinition>();
        foreach (ProductOutputDefinition output in _menu.SelectMany(static entry => entry.Outputs()))
        {
            if (byType.TryGetValue(output.ResultType, out ProductOutputDefinition? registered))
            {
                if (!registered.Source.Equals(output.Source))
                {
                    throw new InvalidOperationException(
                        $"Product '{_manifest.Id}' renders '{output.ResultType.FullName}' with two different renderers; "
                        + "commands that share a result type share one renderer method.");
                }
                continue;
            }
            byType.Add(output.ResultType, output);
            ordered.Add(output);
        }
        return Array.AsReadOnly(ordered.ToArray());
    }

    private IReadOnlyList<ProductOperationCommand> Operations() =>
        Array.AsReadOnly(_menu.SelectMany(static entry => entry.Operations("")).ToArray());

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
            .SelectMany(static format => format.RoutedExtensions)
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
