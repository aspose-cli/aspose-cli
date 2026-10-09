using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using static Aspose.Cli.Product.Cells.Engine.CellsEngineSupport;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary><c>cells query range</c>: a windowed projection of one sheet's cells (ladder step two).</summary>
internal static class CellsRead
{
    public static WorkbookReadResult Run(CellsSession session, ReadRequest request)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.Input);

        LicenseState licenseState = session.Outputs.License;
        using LoadedWorkbook loaded = session.Loader.Open(request.Input, request.Password);
        Workbook workbook = loaded.Workbook;
        (SheetProjection sheet, IReadOnlyDictionary<string, StyleData>? styles, ResultWindow window) =
            ReadProjection.Project(session.Budgets, workbook, request);

        return new WorkbookReadResult
        {
            Source = BuildSource(request.Input, workbook),
            Scope = request.Scope.ToContractName(),
            Sheet = sheet,
            Styles = styles,
            // The window's next is left unset: the read command spells the follow-up
            // command, keeping the engine free of CLI syntax.
            Window = window,
            License = EnvelopeParts.License(licenseState),
            Warnings = loaded.Warnings(loaded.SkippedSheetWarning(request.SheetName is null)),
        };
    }
}
