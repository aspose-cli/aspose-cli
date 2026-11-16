using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>The one rendering contract of a product: the views of its live display.</summary>
/// <typeparam name="TSession">The product's engine session.</typeparam>
public interface IProductViewAdapter<TSession>
    where TSession : class
{
    /// <summary>Views the product renders.</summary>
    IReadOnlyList<ProductView> Views { get; }

    /// <summary>View used for live display.</summary>
    string LiveView { get; }

    /// <summary>Renders the parts of one view into the bounded sink, opening the source once.</summary>
    ViewManifest Render(
        TSession session,
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts);
}

/// <summary>Type-erased host boundary for one strongly typed view adapter.</summary>
public sealed partial class ProductViewDefinition
{
    private readonly Func<object, string, ViewRenderRequest, IViewArtifactSink, ViewManifest> _render;
    private readonly Lazy<ViewPresentation> _presentation;

    private ProductViewDefinition(
        Type sessionType,
        string productId,
        IReadOnlyList<ProductView> views,
        string liveView,
        Func<object, string, ViewRenderRequest, IViewArtifactSink, ViewManifest> render,
        Assembly presenterAssembly)
    {
        SessionType = sessionType;
        ProductId = productId;
        Views = Array.AsReadOnly(views.ToArray());
        LiveView = liveView;
        _render = render;
        _presentation = new Lazy<ViewPresentation>(() => ReadPresentation(presenterAssembly, productId));
    }

    /// <summary>Product id that owns this adapter.</summary>
    public string ProductId { get; }

    /// <summary>The exact product session type this adapter requires.</summary>
    public Type SessionType { get; }

    /// <summary>Every view the product renders.</summary>
    public IReadOnlyList<ProductView> Views { get; }

    /// <summary>Default view of live display.</summary>
    public string LiveView { get; }

    /// <summary>
    /// The product presenter embedded as <c>Presenter/presenter.js</c> and the
    /// optional <c>Presenter/presenter.css</c>, read on first use.
    /// </summary>
    public ViewPresentation Presentation => _presentation.Value;

    /// <summary>Renders one view and validates the product's manifest.</summary>
    public ViewManifest Render(
        ProductBinding binding,
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(artifacts);
        object session = Session(binding);
        if (!Views.Any(view => string.Equals(view.Id, request.View, StringComparison.Ordinal)))
        {
            throw CliErrors.OptionInvalid(
                "--view",
                $"view '{request.View}' is not supported by {ProductId}",
                $"Use {string.Join(", ", Views.Select(static view => view.Id))}.");
        }
        ArgumentOutOfRangeException.ThrowIfLessThan(request.MaxPartCount, 1);
        // An evaluation limit the engine raises is reported as a command's is.
        ViewManifest manifest = binding.Publishing.ReportingEvaluationLimits(() => _render(session, filePath, request, artifacts))
            ?? throw new InvalidOperationException(
                $"Product '{ProductId}' returned no view manifest.");
        Validate(manifest, request);
        // The view discloses an input evaluation mode cut short as a command's result does.
        return binding.Publishing.InputTruncation is { } truncated
            && !(manifest.Warnings ?? []).Any(warning => warning.Code == truncated.Code)
            ? manifest with { Warnings = [.. manifest.Warnings ?? [], truncated] }
            : manifest;
    }

    private static ViewPresentation ReadPresentation(Assembly assembly, string productId) =>
        new(
            ReadResource(assembly, "Presenter/presenter.js")
                ?? throw new InvalidOperationException(
                    $"Product '{productId}' ships no Presenter/presenter.js resource."),
            ReadResource(assembly, "Presenter/presenter.css"));

    private static string? ReadResource(Assembly assembly, string name)
    {
        using Stream? stream = assembly.GetManifestResourceStream(name);
        if (stream is null)
        {
            return null;
        }
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private object Session(ProductBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (binding.ProductId != ProductId)
        {
            throw new InvalidOperationException(
                $"Binding '{binding.ProductId}' cannot render views for product '{ProductId}'.");
        }
        return binding.UntypedSession;
    }

    private void Validate(ViewManifest manifest, ViewRenderRequest request)
    {
        if (!string.Equals(manifest.View, request.View, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Product '{ProductId}' rendered view '{manifest.View}' instead of '{request.View}'.");
        }
        try
        {
            ViewManifestValidator.Validate(manifest, request.MaxPartCount);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"Product '{ProductId}' returned an invalid view manifest.",
                exception);
        }
    }

    internal static ProductViewDefinition Create<TSession>(
        IProductViewAdapter<TSession> adapter,
        string productId,
        Func<object, Func<object?>, object?>? guard = null)
        where TSession : class
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        ProductView[] views = adapter.Views.ToArray();
        if (views.Length == 0
            || views.Select(static view => view.Id).Distinct(StringComparer.Ordinal).Count() != views.Length
            || views.Any(static view => !ViewId().IsMatch(view.Id)
                || string.IsNullOrWhiteSpace(view.Label)
                || view.PartKind is not (ViewPartKinds.Image or ViewPartKinds.Html)))
        {
            throw new InvalidOperationException(
                $"Product '{productId}' must declare uniquely named views with a label and a known part kind.");
        }
        if (!views.Any(view => view.Id == adapter.LiveView))
        {
            throw new InvalidOperationException(
                $"Product '{productId}' must declare a live view.");
        }
        return new ProductViewDefinition(
            typeof(TSession),
            productId,
            views,
            adapter.LiveView,
            // The product guard wraps every adapter call, as it wraps every command handler.
            (session, path, request, artifacts) => guard is null
                ? adapter.Render((TSession)session, path, request, artifacts)
                : (ViewManifest)guard(session, () => adapter.Render((TSession)session, path, request, artifacts))!,
            adapter.GetType().Assembly);
    }

    [GeneratedRegex("^[a-z][a-z0-9-]{0,31}$")]
    private static partial Regex ViewId();
}
