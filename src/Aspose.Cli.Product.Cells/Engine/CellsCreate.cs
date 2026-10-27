using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary><c>cells create</c>: a new workbook with the given sheets.</summary>
internal static class CellsCreate
{
    public static CellsCreateResult Run(CellsSession session, CellsCreateRequest request)
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

        WorkbookSave saved = new CellsSavePipeline(session.Outputs).Save(
            workbook,
            request.Output,
            licenseState,
            // A new workbook has no input whose password it could keep.
            OutputProtection.For(request.Output.Format, request.EncryptPassword, new InputProtection(Encrypted: false), inputPassword: null));

        return new CellsCreateResult
        {
            Output = saved.Output,
            Inputs = [],
            Sheets = request.SheetNames,
            License = EnvelopeParts.License(licenseState),
            Warnings = EnvelopeParts.CombineWarnings(saved.Loss.Warnings),
        };
    }
}
