using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells;

internal static class CellsDiagnostics
{
    internal static readonly ErrorCode SheetNotFound = ErrorCode.NotFound("SHEET_NOT_FOUND");
    internal static readonly ErrorCode NameNotFound = ErrorCode.NotFound("NAME_NOT_FOUND");
    internal static readonly ErrorCode ChartNotFound = ErrorCode.NotFound("CHART_NOT_FOUND");
    internal static readonly ErrorCode PivotNotFound = ErrorCode.NotFound("PIVOT_NOT_FOUND");
    internal static readonly ErrorCode CommentNotFound = ErrorCode.NotFound("COMMENT_NOT_FOUND");
    internal static readonly ErrorCode HyperlinkNotFound = ErrorCode.NotFound("HYPERLINK_NOT_FOUND");
    internal static readonly ErrorCode RangeInvalid =
        new("RANGE_INVALID", ExitCode.ValidationError);
    /// <summary>An explicit read range exceeds the --max-cells budget.</summary>
    internal static readonly ErrorCode RangeTooLarge =
        new("RANGE_TOO_LARGE", ExitCode.ValidationError);
    /// <summary>The selected sheet has no content to render.</summary>
    internal static readonly ErrorCode RenderEmpty =
        new("RENDER_EMPTY", ExitCode.ValidationError);

    internal const string SheetsDropped = "SHEETS_DROPPED";
    internal const string SheetsSkipped = "SHEETS_SKIPPED";
    internal const string DataTruncated = "DATA_TRUNCATED";
    internal const string MhtmlResourceCoverageUnverified = "MHTML_RESOURCE_COVERAGE_UNVERIFIED";
    internal const string FormulasBroken = "FORMULAS_BROKEN";
    internal const string EncryptionRemoved = "WORKBOOK_ENCRYPTION_REMOVED";
    internal const string FormulasCalculatedOnOpen = "FORMULAS_CALCULATED_ON_OPEN";
    internal const string SheetPartiallyRendered = "CELLS_SHEET_PARTIALLY_RENDERED";

    internal static IReadOnlyList<DiagnosticDescriptor> All { get; } =
    [
        Error(SheetNotFound),
        Error(NameNotFound),
        Error(ChartNotFound),
        Error(PivotNotFound),
        Error(CommentNotFound),
        Error(HyperlinkNotFound),
        Error(RangeInvalid),
        Error(RangeTooLarge),
        Error(RenderEmpty),
        Warning(SheetsDropped),
        Warning(SheetsSkipped),
        Warning(DataTruncated),
        Warning(FormulasBroken),
        Warning(EncryptionRemoved),
        Warning(FormulasCalculatedOnOpen),
        Warning(SheetPartiallyRendered),
        Warning(MhtmlResourceCoverageUnverified),
    ];

    private static DiagnosticDescriptor Error(ErrorCode code) =>
        DiagnosticDescriptor.Error(code, "cells", "validation");

    private static DiagnosticDescriptor Warning(string code) =>
        DiagnosticDescriptor.Warning(code, "cells", "warning");
}
