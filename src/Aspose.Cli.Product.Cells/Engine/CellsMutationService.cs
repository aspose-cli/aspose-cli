using Aspose.Cli.Sdk.Errors;
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
    private readonly ResourceBudgetLedger _budgets;
    private readonly CellsEditVerifier _verifier;

    internal CellsMutationService(
        ILicenseGate licenseGate,
        WorkbookLoadService loader,
        WorkbookSaveService saver,
        ResourceBudgetLedger budgets,
        CellsEditVerifier verifier)
    {
        ArgumentNullException.ThrowIfNull(licenseGate);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(saver);
        _licenseGate = licenseGate;
        _loader = loader;
        _saver = saver;
        _budgets = budgets;
        _verifier = verifier;
    }

    /// <inheritdoc />
    internal EditResult ApplyOps(string filePath, OpsBatch batch, EditRequest options)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(options);
        string format = CellsFormats.ForOutputPath(options.OutputPath);
        if (!CellsFormats.EditIds.Contains(format, StringComparer.Ordinal))
        { throw CliErrors.FormatUnsupported(format, CellsFormats.EditIds); }
        batch = CellsOps.Catalog.Prepare(batch);
        using AtomicOutputSetWriter? transaction = options.Options.DryRun ? null
            : _saver.CreateOutputSet([Path.GetDirectoryName(options.OutputPath)!], "cells-edit", options.BackupPath);

        LicenseState licenseState = _licenseGate.EnsureApplied();
        FileWritePrecondition precondition = FileWritePrecondition.Capture(filePath);
        using InputResourceScope operationInputs = _budgets.Inputs.CreateScope();
        using LoadedWorkbook loaded = _loader.Open(filePath, options.Password);
        Workbook workbook = loaded.Workbook;
        SourceInfo input = BuildSource(filePath, workbook);
        FileFingerprints.EnsureUnchanged(filePath, precondition.Fingerprint, input.Fingerprint!);
        FileFingerprints.EnsureMatch(
            filePath,
            options.Options.IfMatch,
            input.Fingerprint!);

        using CellsEditBaseline? baseline = options.Verify
            ? CellsEditBaseline.Capture(filePath, precondition, _budgets) : null;
        WorkbookSavePlan savePlan = WorkbookSavePlan.Create(format, options.OutputPath, licenseState, options.EncryptPassword,
            loaded.IsEncrypted ? options.Password : null);
        IReadOnlyList<BoundedOperationOutcome> applied = OpsExecutor.Execute(
            workbook,
            batch,
            options.Options.BestEffort,
            options.OpSecrets, operationInputs, _budgets);
        if (options.Recalculate)
        {
            workbook.CalculateFormula();
        }

        WorkbookStagedSave? saved = null;
        EditVerification? verification = null;
        if (transaction is not null)
        {
            saved = _saver.Stage(transaction, workbook, savePlan, options.OutputPath, options.Overwrite,
                options.BackupPath, precondition, verifyReopen: true);
            if (options.Verify)
            {
                verification = _verifier.Verify(saved.Candidate, baseline!.Path, filePath,
                    options.Password, savePlan.OutputPassword, batch,
                    CombineWarnings(licenseState, loaded.Resources.CoverageWarning, saved.Truncated, saved.FormulasBroken, saved.SheetsDropped, savePlan.EncryptionWarning));
            }
            transaction.Commit();
        }

        return new EditResult
        {
            Input = input,
            Output = saved?.Output,
            DryRun = options.Options.DryRun,
            Recalculated = options.Recalculate,
            Applied = applied,
            Backup = saved?.Backup,
            Verification = verification,
            License = EnvelopeParts.License(licenseState),
            Warnings = options.Options.DryRun ? loaded.Warnings()
                : CombineWarnings(licenseState, loaded.Resources.CoverageWarning, saved?.Truncated, saved?.FormulasBroken, saved?.SheetsDropped, savePlan.EncryptionWarning),
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

        (OutputInfo output, _, Warning? truncated, Warning? formulasBroken, Warning? sheetsDropped) = _saver.Save(
            workbook,
            request.OutputPath,
            request.Overwrite,
            licenseState,
            request.EncryptPassword);

        return new CreateResult
        {
            Output = output,
            Sheets = request.SheetNames,
            License = EnvelopeParts.License(licenseState),
            Warnings = CombineWarnings(licenseState, truncated, formulasBroken, sheetsDropped),
        };
    }

}

