using Aspose.Cells;
using Aspose.Cli.Product.Cells.Addressing;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Product.Cells.Operations;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.IO;
using static Aspose.Cli.Product.Cells.Engine.CellsEngineSupport;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>Owns workbook creation and bounded mutation.</summary>
internal sealed class CellsMutationService
{
    private readonly ILicenseGate _licenseGate;
    private readonly WorkbookLoadService _loader;
    private readonly WorkbookSaveService _saver;

    internal CellsMutationService(
        ILicenseGate licenseGate,
        WorkbookLoadService loader,
        WorkbookSaveService saver)
    {
        ArgumentNullException.ThrowIfNull(licenseGate);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(saver);
        _licenseGate = licenseGate;
        _loader = loader;
        _saver = saver;
    }

    /// <inheritdoc />
    internal EditResult ApplyOps(string filePath, OpsBatch batch, EditRequest options)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(options);
        batch = OpsParser.Prepare(batch);

        LicenseState licenseState = _licenseGate.EnsureApplied();
        FileWritePrecondition precondition = FileWritePrecondition.Capture(filePath);
        using var workbook = _loader.Open(filePath, options.Password);
        SourceInfo input = BuildSource(filePath, workbook);
        FileFingerprints.EnsureUnchanged(filePath, precondition.Fingerprint, input.Fingerprint!);
        FileFingerprints.EnsureMatch(
            filePath,
            options.Options.IfMatch,
            input.Fingerprint!);

        IReadOnlyList<BoundedOperationOutcome> applied = OpsExecutor.Execute(
            workbook,
            batch,
            options.Options.BestEffort);
        bool explicitlyRecalculated = batch.Ops.Any(static op => op is RecalculateOp);
        if (options.Recalculate && !explicitlyRecalculated)
        {
            workbook.CalculateFormula();
        }

        OutputInfo? output = null;
        BackupInfo? backup = null;
        Warning? truncated = null;
        Warning? formulasBroken = null;
        if (!options.Options.DryRun)
        {
            (output, backup, truncated, formulasBroken) = _saver.Save(
                workbook,
                options.OutputPath,
                options.Overwrite,
                options.EncryptPassword,
                options.BackupPath,
                precondition,
                verifyReopen: true);
        }

        return new EditResult
        {
            Input = input,
            Output = output,
            DryRun = options.Options.DryRun,
            Recalculated = options.Recalculate || explicitlyRecalculated,
            Applied = applied,
            Backup = backup,
            License = EnvelopeParts.License(licenseState),
            Warnings = options.Options.DryRun ? null : CombineWarnings(licenseState, truncated, formulasBroken),
        };
    }

    /// <inheritdoc />
    internal CreateResult CreateWorkbook(NewWorkbookRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        LicenseState licenseState = _licenseGate.EnsureApplied();
        using var workbook = new Workbook();

        workbook.Worksheets[0].Name = request.SheetNames[0];
        foreach (string name in request.SheetNames.Skip(1))
        {
            workbook.Worksheets[workbook.Worksheets.Add()].Name = name;
        }

        (OutputInfo output, _, Warning? truncated, Warning? formulasBroken) = _saver.Save(
            workbook,
            request.OutputPath,
            request.Overwrite,
            request.EncryptPassword);

        return new CreateResult
        {
            Output = output,
            Sheets = request.SheetNames,
            License = EnvelopeParts.License(licenseState),
            Warnings = CombineWarnings(licenseState, truncated, formulasBroken),
        };
    }

}

