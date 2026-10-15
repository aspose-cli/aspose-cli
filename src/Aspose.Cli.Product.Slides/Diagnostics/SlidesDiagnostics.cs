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

    /// <summary>A conversion of every slide to an export format left the hidden slides out.</summary>
    internal static readonly WarningCode HiddenSlidesSkipped = new("HIDDEN_SLIDES_SKIPPED");

    /// <summary>A slide that took another design lost its own background.</summary>
    internal static readonly WarningCode SlideBackgroundReset = new("SLIDE_BACKGROUND_RESET");

    /// <summary>The verified output does not hold the slides in the order the batch left them.</summary>
    internal static readonly DiagnosticDescriptor SlideOrderMismatch = DiagnosticDescriptor.Verification("SLIDE_ORDER_MISMATCH");

    /// <summary>A slide of the verified output does not show the title, text, notes, layout, visibility or sections the batch set.</summary>
    internal static readonly DiagnosticDescriptor SlideContentMismatch = DiagnosticDescriptor.Verification("SLIDE_CONTENT_MISMATCH");

    /// <summary>The verified output does not hold the document properties or slide size the batch set.</summary>
    internal static readonly DiagnosticDescriptor PresentationSettingMismatch = DiagnosticDescriptor.Verification("PRESENTATION_SETTING_MISMATCH");

    internal static IReadOnlyList<DiagnosticDescriptor> All { get; } =
    [
        DiagnosticDescriptor.Error(SlideNotFound),
        DiagnosticDescriptor.Error(PlaceholderNotFound),
        DiagnosticDescriptor.Error(ChartDataInvalid),
        DiagnosticDescriptor.Error(ShapeNotFound),
        DiagnosticDescriptor.Error(LayoutNotFound),
        DiagnosticDescriptor.Warning(ChartTitleOverlaid),
        DiagnosticDescriptor.Warning(TableOverflow),
        DiagnosticDescriptor.Warning(SlideBackgroundReset),
        DiagnosticDescriptor.Warning(HiddenSlidesSkipped),
        SlideOrderMismatch,
        SlideContentMismatch,
        PresentationSettingMismatch,
    ];

    private static ErrorCode Validation(string code) =>
        new(code, ExitCode.ValidationError);
}
