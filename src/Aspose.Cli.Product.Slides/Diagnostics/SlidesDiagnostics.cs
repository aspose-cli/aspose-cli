using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Slides;

internal static class SlidesDiagnostics
{
    internal static readonly ErrorCode SlideNotFound = ErrorCode.NotFound("SLIDE_NOT_FOUND");
    internal static readonly ErrorCode PlaceholderNotFound = ErrorCode.NotFound("PLACEHOLDER_NOT_FOUND");
    internal static readonly ErrorCode ChartDataInvalid = Validation("CHART_DATA_INVALID");
    internal static readonly ErrorCode ShapeNotFound = ErrorCode.NotFound("SHAPE_NOT_FOUND");
    internal static readonly ErrorCode LayoutNotFound = ErrorCode.NotFound("LAYOUT_NOT_FOUND");

    /// <summary>An output draws a chart's implicit automatic title over its plot.</summary>
    internal static readonly WarningCode ChartTitleOverlaid = new("CHART_TITLE_OVERLAID");

    /// <summary>An authored table is taller than the area it was placed in.</summary>
    internal static readonly WarningCode TableOverflow = new("TABLE_OVERFLOW");

    /// <summary>A slide that took another design lost its own background.</summary>
    internal static readonly WarningCode SlideBackgroundReset = new("SLIDE_BACKGROUND_RESET");

    internal static IReadOnlyList<DiagnosticDescriptor> All { get; } =
    [
        Error(SlideNotFound),
        Error(PlaceholderNotFound),
        Error(ChartDataInvalid),
        Error(ShapeNotFound),
        Error(LayoutNotFound),
        DiagnosticDescriptor.Warning(ChartTitleOverlaid, "slides"),
        DiagnosticDescriptor.Warning(TableOverflow, "slides"),
        DiagnosticDescriptor.Warning(SlideBackgroundReset, "slides"),
    ];

    private static ErrorCode Validation(string code) =>
        new(code, ExitCode.ValidationError);

    private static DiagnosticDescriptor Error(ErrorCode code) =>
        DiagnosticDescriptor.Error(code, "slides");
}
