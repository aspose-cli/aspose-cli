using System.Text.Json;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>Product-owned browser presentation used by the shared server.</summary>
public sealed record ProductPreviewPresentation(
    string ClientScript,
    string ShellStylesheet);

/// <summary>Stable preview view identity and its user-facing label.</summary>
public sealed record ProductPreviewView(string Id, string DisplayName);

/// <summary>
/// Strongly typed product preview semantics. HTTP, SSE, process lifecycle and
/// last-good snapshots remain exclusively in the host.
/// </summary>
/// <typeparam name="TPort">The product's typed port.</typeparam>
public interface IProductPreviewAdapter<TPort>
    where TPort : class
{
    /// <summary>Default view id for this product.</summary>
    string DefaultView { get; }

    /// <summary>Supported views with display metadata.</summary>
    IReadOnlyList<ProductPreviewView> ViewDefinitions { get; }

    /// <summary>Product-owned selector, hint, and state payload contracts.</summary>
    IReadOnlyList<ProductPreviewPayloadContract> PayloadContracts { get; }

    /// <summary>Creates a renderer backed by only this product's port.</summary>
    PreviewRenderer CreateRenderer(
        TPort port,
        string filePath,
        ProductPreviewRequest request);

    /// <summary>Returns the product-owned browser assets.</summary>
    ProductPreviewPresentation CreatePresentation(string? presentationEffect);

    /// <summary>Validates product-specific preview arguments.</summary>
    void ValidateRequest(
        ProductPreviewRequest request,
        string? presentationEffect);

    /// <summary>Validates domain fields after the neutral envelope is checked.</summary>
    void ValidatePayload(ProductPreviewPayload payload);
}

/// <summary>Type-erased host bridge created from a typed preview adapter.</summary>
public sealed class ProductPreviewDefinition
{
    private readonly IReadOnlyList<string> _views;
    private readonly Func<object, string, ProductPreviewRequest, PreviewRenderer>
        _createRenderer;
    private readonly Func<string?, ProductPreviewPresentation> _createPresentation;
    private readonly Action<ProductPreviewRequest, string?> _validate;
    private readonly Action<ProductPreviewPayload> _validatePayload;

    private ProductPreviewDefinition(
        Type portType,
        string productId,
        string defaultView,
        IReadOnlyList<ProductPreviewView> views,
        IReadOnlyList<ProductPreviewPayloadContract> payloadContracts,
        Func<object, string, ProductPreviewRequest, PreviewRenderer> createRenderer,
        Func<string?, ProductPreviewPresentation> createPresentation,
        Action<ProductPreviewRequest, string?> validate,
        Action<ProductPreviewPayload> validatePayload)
    {
        PortType = portType;
        ProductId = productId;
        DefaultView = defaultView;
        ViewDefinitions = Array.AsReadOnly(
            views.Select(static view => view with { }).ToArray());
        _views = Array.AsReadOnly(
            ViewDefinitions.Select(static view => view.Id).ToArray());
        PayloadContracts = Array.AsReadOnly(
            payloadContracts.Select(static contract => contract with { }).ToArray());
        _createRenderer = createRenderer;
        _createPresentation = createPresentation;
        _validate = validate;
        _validatePayload = validatePayload;
    }

    /// <summary>Product id that owns this adapter and all of its payloads.</summary>
    public string ProductId { get; }

    /// <summary>The exact product port required by this adapter.</summary>
    public Type PortType { get; }

    /// <summary>Default view id.</summary>
    public string DefaultView { get; }

    /// <summary>Supported view ids.</summary>
    public IReadOnlyList<string> Views => _views;

    /// <summary>Supported views and their product-owned display labels.</summary>
    public IReadOnlyList<ProductPreviewView> ViewDefinitions { get; }

    /// <summary>Versioned product-owned payload contracts.</summary>
    public IReadOnlyList<ProductPreviewPayloadContract> PayloadContracts { get; }

    /// <summary>Whether this product accepts interactive state publications.</summary>
    public bool SupportsState => PayloadContracts.Any(static contract =>
        string.Equals(
            contract.Kind,
            ProductPreviewPayloadKinds.State,
            StringComparison.Ordinal));

    /// <summary>Creates a renderer from the matching activated binding.</summary>
    public PreviewRenderer CreateRenderer(
        ProductBinding binding,
        string filePath,
        ProductPreviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (binding.ProductId != ProductId || binding.PortType != PortType)
        {
            throw new InvalidOperationException(
                $"Binding '{binding.ProductId}' cannot render preview for product '{ProductId}'.");
        }
        return _createRenderer(binding.UntypedPort, filePath, request);
    }

    /// <summary>Returns product-owned preview assets.</summary>
    public ProductPreviewPresentation CreatePresentation(
        string? presentationEffect) =>
        _createPresentation(presentationEffect);

    /// <summary>Validates product-specific preview arguments.</summary>
    public void ValidateRequest(
        ProductPreviewRequest request,
        string? presentationEffect)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Selector is not null)
        {
            ValidatePayload(
                request.Selector,
                ProductPreviewPayloadKinds.Selector);
        }
        _validate(request, presentationEffect);
    }

    /// <summary>
    /// Validates ownership, kind, version, schema and size before dispatching
    /// the opaque value to product code.
    /// </summary>
    public void ValidatePayload(
        ProductPreviewPayload payload,
        string? expectedKind = null)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (!string.Equals(
                payload.ProductId,
                ProductId,
                StringComparison.Ordinal))
        {
            throw CliErrors.OptionInvalid(
                "preview payload",
                $"payload owner '{payload.ProductId}' does not match product '{ProductId}'",
                $"Use a payload declared by the {ProductId} preview adapter.");
        }
        if (expectedKind is not null
            && !string.Equals(
                payload.Kind,
                expectedKind,
                StringComparison.Ordinal))
        {
            throw CliErrors.OptionInvalid(
                "preview payload",
                $"payload kind '{payload.Kind}' cannot be used as '{expectedKind}'",
                $"Use a {expectedKind} payload.");
        }

        ProductPreviewPayloadContract? contract = PayloadContracts
            .FirstOrDefault(item =>
                string.Equals(
                    item.Kind,
                    payload.Kind,
                    StringComparison.Ordinal)
                && item.SchemaVersion == payload.SchemaVersion);
        if (contract is null)
        {
            string supported = string.Join(
                ", ",
                PayloadContracts.Select(static item =>
                    $"{item.Kind}@v{item.SchemaVersion}"));
            throw CliErrors.OptionInvalid(
                "preview payload",
                $"unsupported payload kind/version '{payload.Kind}@v{payload.SchemaVersion}'",
                supported.Length == 0
                    ? $"The {ProductId} preview declares no product payloads."
                    : $"Use one of: {supported}.");
        }
        if (!string.Equals(
                contract.SchemaId,
                payload.SchemaId,
                StringComparison.Ordinal))
        {
            throw CliErrors.OptionInvalid(
                "preview payload",
                $"schema '{payload.SchemaId}' does not match '{contract.SchemaId}'",
                $"Use schema '{contract.SchemaId}' for {payload.Kind}@v{payload.SchemaVersion}.");
        }
        if (payload.Payload.ValueKind is JsonValueKind.Undefined
            or JsonValueKind.Null)
        {
            throw CliErrors.OptionInvalid(
                "preview payload",
                "the product payload is null",
                "Provide a JSON object accepted by the declared product schema.");
        }
        if (payload.Utf8Bytes > contract.MaxBytes)
        {
            throw CliErrors.OptionInvalid(
                "preview payload",
                $"the payload is {payload.Utf8Bytes} bytes; the limit is {contract.MaxBytes}",
                "Reduce the selector, hint, or state payload.");
        }
        BoundedJsonValidation.ValidateNoDuplicateProperties(
            payload.Payload,
            static message => new JsonException(message));
        _validatePayload(payload);
    }

    internal static ProductPreviewDefinition Create<TPort>(
        IProductPreviewAdapter<TPort> adapter,
        string productId)
        where TPort : class
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        return new ProductPreviewDefinition(
            typeof(TPort),
            productId,
            adapter.DefaultView,
            adapter.ViewDefinitions.ToArray(),
            adapter.PayloadContracts.ToArray(),
            (port, path, request) =>
                adapter.CreateRenderer((TPort)port, path, request),
            adapter.CreatePresentation,
            adapter.ValidateRequest,
            adapter.ValidatePayload);
    }
}
