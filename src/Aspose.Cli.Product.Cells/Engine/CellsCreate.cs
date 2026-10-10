using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary><c>cells create</c>: a new workbook with the given sheets.</summary>
internal static class CellsCreate
{
    public static CreateResult Run(CellsSession session, NewWorkbookRequest request)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);

        LicenseState licenseState = session.Outputs.License;
        using var workbook = new Workbook();

        workbook.Worksheets[0].Name = request.SheetNames[0];
        foreach (string name in request.SheetNames.Skip(1))
        {
            workbook.Worksheets[workbook.Worksheets.Add()].Name = name;
        }

        WorkbookStagedSave saved = new CellsSavePipeline(session.Outputs, session.Loader).Save(
            workbook,
            request.Output,
            licenseState,
            request.EncryptPassword);

        return new CreateResult
        {
            Output = saved.Output,
            Sheets = request.SheetNames,
            License = EnvelopeParts.License(licenseState),
            Warnings = EnvelopeParts.CombineWarnings(saved.Truncated, saved.FormulasBroken, saved.SheetsDropped),
        };
    }
}
