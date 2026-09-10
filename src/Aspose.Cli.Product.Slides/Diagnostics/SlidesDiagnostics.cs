using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Slides;

internal static class SlidesDiagnostics
{
    internal static readonly ErrorCode SlideNotFound = Validation("SLIDE_NOT_FOUND");
    internal static readonly ErrorCode PlaceholderNotFound = Validation("PLACEHOLDER_NOT_FOUND");
    internal static readonly ErrorCode ChartDataInvalid = Validation("CHART_DATA_INVALID");

    internal static IReadOnlyList<DiagnosticDescriptor> All { get; } =
    [
        Error(SlideNotFound),
        Error(PlaceholderNotFound),
        Error(ChartDataInvalid),
    ];

    private static ErrorCode Validation(string code) =>
        new(code, ExitCode.ValidationError);

    private static DiagnosticDescriptor Error(ErrorCode code) =>
        DiagnosticDescriptor.Error(code, "slides", "validation");
}
