using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Ports;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>Product-neutral request for one static review evidence render.</summary>
public sealed record ProductReviewRequest(
    string View,
    int MaxItems,
    string? Password = null,
    FontSearchProfile? FontProfile = null);

/// <summary>Product-owned facts returned after static evidence is rendered.</summary>
public sealed record ProductReviewRenderOutcome(
    string EntryFileName,
    string SourceFormatId,
    long SourceSizeBytes)
{
    public bool VisualInspectionRequired { get; init; } = true;

    public IReadOnlyList<Contracts.ReviewFinding>? Findings { get; init; }

    public IReadOnlyList<Contracts.ReviewCoverageMetric>? Coverage { get; init; }

    public required int ExpectedItems { get; init; }

    public required int RenderedItems { get; init; }

    public required bool Complete { get; init; }
}

/// <summary>Renders product evidence into an existing empty directory.</summary>
public delegate ProductReviewRenderOutcome ProductReviewRenderer(
    string evidenceDirectory);

/// <summary>Strongly typed static review adapter owned by one product.</summary>
public interface IProductReviewAdapter<TPort>
    where TPort : class
{
    string DefaultView { get; }

    IReadOnlyList<string> Views { get; }

    bool VisualInspectionRequired { get; }

    ProductReviewRenderer CreateRenderer(
        TPort port,
        string filePath,
        ProductReviewRequest request);
}

/// <summary>Type-erased Host boundary for one strongly typed review adapter.</summary>
public sealed class ProductReviewDefinition
{
    private readonly Func<object, string, ProductReviewRequest, ProductReviewRenderer>
        _createRenderer;

    private ProductReviewDefinition(
        Type portType,
        string productId,
        string defaultView,
        IReadOnlyList<string> views,
        bool visualInspectionRequired,
        Func<object, string, ProductReviewRequest, ProductReviewRenderer> createRenderer)
    {
        PortType = portType;
        ProductId = productId;
        DefaultView = defaultView;
        Views = Array.AsReadOnly(views.ToArray());
        VisualInspectionRequired = visualInspectionRequired;
        _createRenderer = createRenderer;
    }

    public string ProductId { get; }

    public Type PortType { get; }

    public string DefaultView { get; }

    public IReadOnlyList<string> Views { get; }

    public bool VisualInspectionRequired { get; }

    public ProductReviewRenderer CreateRenderer(
        ProductBinding binding,
        string filePath,
        ProductReviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(request);
        if (binding.ProductId != ProductId || binding.PortType != PortType)
        {
            throw new InvalidOperationException(
                $"Binding '{binding.ProductId}' cannot render review evidence for product '{ProductId}'.");
        }
        if (!Views.Contains(request.View, StringComparer.Ordinal))
        {
            throw CliErrors.OptionInvalid(
                "--view",
                $"review view '{request.View}' is not supported by {ProductId}",
                $"Use {string.Join(", ", Views)}.");
        }
        ProductReviewRenderer renderer = _createRenderer(
            binding.UntypedPort,
            filePath,
            request);
        return directory =>
        {
            ProductReviewRenderOutcome outcome = renderer(directory);
            IFontEnvironment? fonts = binding.FontEnvironment;
            return fonts is null
                ? AppendFontCheckUnavailable(outcome)
                : AppendFontFindings(
                    outcome,
                    fonts.CheckFonts(filePath, new FontCheckRequest
                    {
                        Password = request.Password,
                        FontProfile = request.FontProfile,
                    }));
        };
    }

    private static ProductReviewRenderOutcome AppendFontCheckUnavailable(
        ProductReviewRenderOutcome outcome) =>
        outcome with
        {
            Findings =
            [
                .. outcome.Findings ?? [],
                new ReviewFinding
                {
                    Code = "FONTS_NOT_CHECKED",
                    Severity = "warning",
                    Message = "The product engine does not expose font diagnostics, so used fonts were not checked.",
                    Location = "document",
                    Hint = "Verify font availability and substitution on the target system before relying on visual fidelity.",
                },
            ],
        };

    private static ProductReviewRenderOutcome AppendFontFindings(
        ProductReviewRenderOutcome outcome,
        FontCheckResult fonts)
    {
        FontAvailability[] unavailable = fonts.Fonts
            .Where(static font => !font.Available)
            .ToArray();
        if (unavailable.Length == 0)
        {
            return outcome;
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
        var finding = new ReviewFinding
        {
            Code = "FONTS_MISSING_OR_SUBSTITUTED",
            Severity = "error",
            Message = $"{unavailable.Length} used font(s) are unavailable or substituted: {summary}.",
            Location = "document",
            Hint = "Install the required fonts or deliberately replace them, then save and run review again in a new directory.",
        };
        return outcome with
        {
            Findings = [.. outcome.Findings ?? [], finding],
            Complete = false,
        };
    }

    internal static ProductReviewDefinition Create<TPort>(
        IProductReviewAdapter<TPort> adapter,
        string productId)
        where TPort : class
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        if (adapter.Views.Count == 0
            || !adapter.Views.Contains(adapter.DefaultView, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Product '{productId}' review adapter must declare its default view.");
        }
        return new ProductReviewDefinition(
            typeof(TPort),
            productId,
            adapter.DefaultView,
            adapter.Views,
            adapter.VisualInspectionRequired,
            (port, path, request) =>
                adapter.CreateRenderer((TPort)port, path, request));
    }

}
