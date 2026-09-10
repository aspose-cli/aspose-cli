using System.Reflection;
using System.Text;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>
/// Owns the invariant view metadata, embedded browser assets, and request
/// lifecycle shared by every product preview adapter.
/// </summary>
/// <typeparam name="TPort">The product's typed engine port.</typeparam>
public abstract class ProductPreviewAdapterBase<TPort>
    : IProductPreviewAdapter<TPort>
    where TPort : class
{
    private const string ClientResource = "Preview/live-client.js";
    private const string StylesheetResource = "Preview/shell.css";

    private readonly string _productName;
    private readonly string _effectHint;
    private readonly Lazy<string> _clientScript;
    private readonly Lazy<string> _shellStylesheet;

    /// <summary>Initializes one adapter from its product assembly and views.</summary>
    protected ProductPreviewAdapterBase(
        Assembly resourceAssembly,
        string productName,
        string defaultView,
        IReadOnlyList<ProductPreviewView> viewDefinitions,
        string effectHint)
    {
        ArgumentNullException.ThrowIfNull(resourceAssembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultView);
        ArgumentNullException.ThrowIfNull(viewDefinitions);
        ArgumentException.ThrowIfNullOrWhiteSpace(effectHint);
        if (viewDefinitions.Count == 0)
        {
            throw new ArgumentException(
                "At least one preview view is required.",
                nameof(viewDefinitions));
        }

        ProductPreviewView[] definitions = viewDefinitions.ToArray();
        foreach (ProductPreviewView view in definitions)
        {
            if (!IsValidViewId(view.Id))
            {
                throw new ArgumentException(
                    $"Preview view id '{view.Id}' must be 1-32 lowercase ASCII letters, digits or hyphens, starting with a letter.",
                    nameof(viewDefinitions));
            }

            if (string.IsNullOrWhiteSpace(view.DisplayName)
                || view.DisplayName.Length > 80
                || view.DisplayName.Contains('\r')
                || view.DisplayName.Contains('\n'))
            {
                throw new ArgumentException(
                    $"Preview view '{view.Id}' has an invalid display name.",
                    nameof(viewDefinitions));
            }
        }

        if (definitions
            .Select(static view => view.Id)
            .Distinct(StringComparer.Ordinal)
            .Count() != definitions.Length)
        {
            throw new ArgumentException(
                "Preview view ids must be unique.",
                nameof(viewDefinitions));
        }

        if (!definitions.Any(view => string.Equals(
                view.Id,
                defaultView,
                StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                $"Default preview view '{defaultView}' is not declared.",
                nameof(defaultView));
        }

        _productName = productName;
        _effectHint = effectHint;
        DefaultView = defaultView;
        ViewDefinitions = definitions;
        _clientScript = new Lazy<string>(() => ReadResource(
            resourceAssembly,
            ClientResource,
            productName));
        _shellStylesheet = new Lazy<string>(() => ReadResource(
            resourceAssembly,
            StylesheetResource,
            productName));
    }

    /// <inheritdoc />
    public string DefaultView { get; }

    /// <inheritdoc />
    public IReadOnlyList<ProductPreviewView> ViewDefinitions { get; }

    /// <inheritdoc />
    public virtual IReadOnlyList<ProductPreviewPayloadContract>
        PayloadContracts => [];

    /// <inheritdoc />
    public abstract PreviewRenderer CreateRenderer(
        TPort port,
        string filePath,
        ProductPreviewRequest request);

    /// <inheritdoc />
    public ProductPreviewPresentation CreatePresentation(string? effect) =>
        new(
            CreateClientScript(effect),
            _shellStylesheet.Value);

    /// <inheritdoc />
    public void ValidateRequest(
        ProductPreviewRequest request,
        string? effect)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateProductRequest(request);
        ValidateEffect(effect);
    }

    /// <inheritdoc />
    public virtual void ValidatePayload(ProductPreviewPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
    }

    /// <summary>Validates the declared view and product-specific semantics.</summary>
    protected virtual void ValidateProductRequest(
        ProductPreviewRequest request)
    {
        if (!ViewDefinitions.Any(view =>
                string.Equals(view.Id, request.View, StringComparison.Ordinal)))
        {
            throw CliErrors.OptionInvalid(
                "--view",
                $"preview view '{request.View}' is not supported by {_productName}",
                $"Use {string.Join(", ", ViewDefinitions.Select(static view => view.Id))}.");
        }
    }

    /// <summary>
    /// Builds the product client script. Products with supported presentation
    /// effects can override this template method.
    /// </summary>
    protected virtual string CreateClientScript(string? effect)
    {
        ValidateEffect(effect);
        return _clientScript.Value;
    }

    /// <summary>Validates presentation effects for this product.</summary>
    protected virtual void ValidateEffect(string? effect)
    {
        if (effect is not null)
        {
            throw CliErrors.OptionInvalid(
                "--fx",
                $"presentation effects are not supported by {_productName} preview",
                _effectHint);
        }
    }

    /// <summary>Returns the shared embedded client script.</summary>
    protected string ClientScript => _clientScript.Value;

    private static bool IsValidViewId(string? value)
    {
        if (string.IsNullOrEmpty(value)
            || value.Length > 32
            || value[0] is < 'a' or > 'z')
        {
            return false;
        }

        return value.All(static character =>
            character is >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '-');
    }

    private static string ReadResource(
        Assembly assembly,
        string name,
        string productName)
    {
        using Stream stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException(
                $"Missing {productName} preview resource '{name}'.");
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
