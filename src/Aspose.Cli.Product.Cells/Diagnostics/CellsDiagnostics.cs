using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells;

internal static class CellsDiagnostics
{
    internal static readonly ErrorCode SheetNotFound =
        new("SHEET_NOT_FOUND", ExitCode.ValidationError);
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
    internal const string ErrorsTruncated = "ERRORS_TRUNCATED";
    internal const string MhtmlResourceCoverageUnverified = "MHTML_RESOURCE_COVERAGE_UNVERIFIED";
    internal const string FormulasBroken = "FORMULAS_BROKEN";
    internal const string EncryptionRemoved = "WORKBOOK_ENCRYPTION_REMOVED";

    internal static IReadOnlyList<DiagnosticDescriptor> All { get; } =
    [
        Error(SheetNotFound),
        Error(RangeInvalid),
        Error(RangeTooLarge),
        Error(RenderEmpty),
        Warning(SheetsDropped),
        Warning(SheetsSkipped),
        Warning(DataTruncated),
        Warning(ErrorsTruncated),
        Warning(FormulasBroken),
        Warning(EncryptionRemoved),
        Warning(MhtmlResourceCoverageUnverified),
    ];

    private static DiagnosticDescriptor Error(ErrorCode code) =>
        DiagnosticDescriptor.Error(code, "cells", "validation");

    private static DiagnosticDescriptor Warning(string code) =>
        DiagnosticDescriptor.Warning(code, "cells", "warning");
}
