using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>Product-owned review facts derived from one rendered view.</summary>
public sealed record ProductReviewAssessment
{
    /// <summary>Deterministic structural or semantic observations.</summary>
    public IReadOnlyList<ReviewFinding>? Findings { get; init; }

    /// <summary>Warnings raised while inspecting the source.</summary>
    public IReadOnlyList<Warning>? Warnings { get; init; }

    /// <summary>Product-owned coverage measurements.</summary>
    public IReadOnlyList<ReviewCoverageMetric>? Coverage { get; init; }

    /// <summary>Whether every deterministic check ran to completion.</summary>
    public bool Complete { get; init; } = true;
}

/// <summary>
/// The one rendering contract of a product: the same views serve static
/// review evidence and live display, so what people watch is exactly what
/// agents review.
/// </summary>
/// <typeparam name="TSession">The product's engine session.</typeparam>
public interface IProductViewAdapter<TSession>
    where TSession : class
{
    /// <summary>Views the product renders.</summary>
    IReadOnlyList<ProductView> Views { get; }

    /// <summary>View used for static review evidence; it must render image parts.</summary>
    string ReviewView { get; }

    /// <summary>View used for live display.</summary>
    string LiveView { get; }

    /// <summary>Whether review evidence always requires human or AI visual inspection.</summary>
    bool VisualInspectionRequired { get; }

    /// <summary>
    /// Every check <see cref="Assess"/> can report, each declared once; codes start with the
    /// product id in upper case. The SDK adds its font checks (<see cref="ReviewChecks"/>).
    /// </summary>
    IReadOnlyList<ReviewCheck> Checks { get; }

    /// <summary>Renders the parts of one view into the bounded sink, opening the source once.</summary>
    ViewManifest Render(
        TSession session,
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts);

    /// <summary>Inspects the source for review findings and coverage beyond the rendered parts.</summary>
    ProductReviewAssessment Assess(
        TSession session,
        string filePath,
        ViewRenderRequest request,
        ViewManifest rendered);
}

/// <summary>Type-erased host boundary for one strongly typed view adapter.</summary>
public sealed partial class ProductViewDefinition
{
    private readonly Func<object, string, ViewRenderRequest, IViewArtifactSink, ViewManifest> _render;
    private readonly Func<object, string, ViewRenderRequest, ViewManifest, ProductReviewAssessment> _assess;
    private readonly Lazy<ViewPresentation> _presentation;

    private ProductViewDefinition(
        Type sessionType,
        string productId,
        IReadOnlyList<ProductView> views,
        string reviewView,
        string liveView,
        bool visualInspectionRequired,
        IReadOnlyList<ReviewCheck> checks,
        Func<object, string, ViewRenderRequest, IViewArtifactSink, ViewManifest> render,
        Func<object, string, ViewRenderRequest, ViewManifest, ProductReviewAssessment> assess,
        Assembly presenterAssembly)
    {
        SessionType = sessionType;
        ProductId = productId;
        Views = Array.AsReadOnly(views.ToArray());
        ReviewView = reviewView;
        LiveView = liveView;
        ReviewViews = Array.AsReadOnly(Views
            .Where(static view => view.PartKind == ViewPartKinds.Image)
            .Select(static view => view.Id)
            .ToArray());
        VisualInspectionRequired = visualInspectionRequired;
        Checks = checks;
        _render = render;
        _assess = assess;
        _presentation = new Lazy<ViewPresentation>(() => ReadPresentation(presenterAssembly, productId));
    }

    /// <summary>Product id that owns this adapter.</summary>
    public string ProductId { get; }

    /// <summary>The exact product session type this adapter requires.</summary>
    public Type SessionType { get; }

    /// <summary>Every view the product renders.</summary>
    public IReadOnlyList<ProductView> Views { get; }

    /// <summary>Default view of static review evidence.</summary>
    public string ReviewView { get; }

    /// <summary>Default view of live display.</summary>
    public string LiveView { get; }

    /// <summary>Views whose image parts can serve as review evidence.</summary>
    public IReadOnlyList<string> ReviewViews { get; }

    /// <summary>Whether review evidence always requires visual inspection.</summary>
    public bool VisualInspectionRequired { get; }

    /// <summary>Every check a review of this product can report, ordered by code.</summary>
    public IReadOnlyList<ReviewCheck> Checks { get; }

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
        ViewManifest manifest = _render(session, filePath, request, artifacts)
            ?? throw new InvalidOperationException(
                $"Product '{ProductId}' returned no view manifest.");
        Validate(manifest, request);
        return manifest;
    }

    /// <summary>Runs the product's review checks and the shared font check.</summary>
    public ProductReviewAssessment Assess(
        ProductBinding binding,
        string filePath,
        ViewRenderRequest request,
        ViewManifest rendered)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(rendered);
        ProductReviewAssessment assessment = _assess(Session(binding), filePath, request, rendered)
            ?? throw new InvalidOperationException(
                $"Product '{ProductId}' returned no review assessment.");
        EnsureDeclared(assessment.Findings);
        IFontEnvironment? fonts = binding.FontEnvironment;
        return fonts is null
            ? AppendFontCheckUnavailable(assessment)
            : AppendFontFindings(
                assessment,
                fonts.CheckFonts(filePath, new FontCheckRequest
                {
                    Password = request.Password,
                }));
    }

    // A finding must come from a declared check with that check's severity; anything else is a
    // product defect, because callers filter and gate on the declared codes.
    private void EnsureDeclared(IReadOnlyList<ReviewFinding>? findings)
    {
        foreach (ReviewFinding finding in findings ?? [])
        {
            if (!Checks.Any(check => check.Code == finding.Code && check.Severity == finding.Severity))
            {
                throw new InvalidOperationException(
                    $"Product '{ProductId}' reported finding '{finding.Code}' ({finding.Severity}), which is not a declared check.");
            }
        }
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

    private static ProductReviewAssessment AppendFontCheckUnavailable(
        ProductReviewAssessment assessment) =>
        assessment with
        {
            Findings =
            [
                .. assessment.Findings ?? [],
                ReviewChecks.FontsNotChecked.Finding(
                    "The product engine does not expose font diagnostics, so used fonts were not checked.",
                    "document",
                    "Verify font availability and substitution on the target system before relying on visual fidelity."),
            ],
        };

    private static ProductReviewAssessment AppendFontFindings(
        ProductReviewAssessment assessment,
        FontCheckResult fonts)
    {
        FontAvailability[] unavailable = fonts.Fonts
            .Where(static font => !font.Available)
            .ToArray();
        if (unavailable.Length == 0)
        {
            return assessment;
        }

        string summary = string.Join(
            ", ",
            unavailable.Take(8).Select(static font => font.SubstitutedBy is null
                ? font.Name
                : $"{font.Name} -> {font.SubstitutedBy}"));
        if (unavailable.Length > 8)
        {
            summary += $", and {unavailable.Length - 8} more";
        }
        ReviewFinding finding = ReviewChecks.FontsMissingOrSubstituted.Finding(
            $"{unavailable.Length} used font(s) are unavailable or substituted: {summary}.",
            "document",
            "Install the required fonts or deliberately replace them, then save and run review again in a new directory.");
        return assessment with
        {
            Findings = [.. assessment.Findings ?? [], finding],
            Complete = false,
        };
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
        if (!views.Any(view => view.Id == adapter.ReviewView && view.PartKind == ViewPartKinds.Image)
            || !views.Any(view => view.Id == adapter.LiveView))
        {
            throw new InvalidOperationException(
                $"Product '{productId}' must declare an image review view and a live view.");
        }
        string prefix = productId.ToUpperInvariant() + "_";
        ReviewCheck[] checks = [.. adapter.Checks, .. ReviewChecks.Shared];
        if (adapter.Checks.Any(check => !check.Code.StartsWith(prefix, StringComparison.Ordinal))
            || checks.Select(static check => check.Code).Distinct(StringComparer.Ordinal).Count() != checks.Length)
        {
            throw new InvalidOperationException(
                $"Product '{productId}' must declare uniquely coded review checks that start with '{prefix}'.");
        }
        return new ProductViewDefinition(
            typeof(TSession),
            productId,
            views,
            adapter.ReviewView,
            adapter.LiveView,
            adapter.VisualInspectionRequired,
            Array.AsReadOnly(checks.OrderBy(static check => check.Code, StringComparer.Ordinal).ToArray()),
            // The product guard wraps every adapter call, as it wraps every command handler.
            (session, path, request, artifacts) => guard is null
                ? adapter.Render((TSession)session, path, request, artifacts)
                : (ViewManifest)guard(session, () => adapter.Render((TSession)session, path, request, artifacts))!,
            (session, path, request, rendered) => guard is null
                ? adapter.Assess((TSession)session, path, request, rendered)
                : (ProductReviewAssessment)guard(session, () => adapter.Assess((TSession)session, path, request, rendered))!,
            adapter.GetType().Assembly);
    }

    [GeneratedRegex("^[a-z][a-z0-9-]{0,31}$")]
    private static partial Regex ViewId();
}
