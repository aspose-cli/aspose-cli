using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Slides;

internal static class SlidesDiagnostics
{
    internal static readonly ErrorCode SlideNotFound = Validation("SLIDE_NOT_FOUND");
    internal static readonly ErrorCode PlaceholderNotFound = Validation("PLACEHOLDER_NOT_FOUND");
    internal static readonly ErrorCode ChartDataInvalid = Validation("CHART_DATA_INVALID");
    internal static readonly ErrorCode ShapeNotFound = Validation("SHAPE_NOT_FOUND");
    internal static readonly ErrorCode LayoutNotFound = Validation("LAYOUT_NOT_FOUND");

    internal static IReadOnlyList<DiagnosticDescriptor> All { get; } =
    [
        Error(SlideNotFound),
        Error(PlaceholderNotFound),
        Error(ChartDataInvalid),
        Error(ShapeNotFound),
        Error(LayoutNotFound),
    ];

    private static ErrorCode Validation(string code) =>
        new(code, ExitCode.ValidationError);

    private static DiagnosticDescriptor Error(ErrorCode code) =>
        DiagnosticDescriptor.Error(code, "slides", "validation");
}
