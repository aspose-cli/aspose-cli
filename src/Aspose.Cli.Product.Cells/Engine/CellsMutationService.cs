using Aspose.Cli.Sdk.Errors;
using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Engine.Editing;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Operations;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.IO;
using static Aspose.Cli.Product.Cells.Engine.CellsEngineSupport;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>Owns bounded mutation: opens a workbook, applies an ops batch and publishes the result.</summary>
internal sealed class CellsMutationService
{
    private readonly ILicenseGate _licenseGate;
    private readonly CellsWorkbookLoader _loader;
    private readonly CellsSavePipeline _saver;
    private readonly ResourceBudgetLedger _budgets;
    private readonly CellsEditVerifier _verifier;

    internal CellsMutationService(
        ILicenseGate licenseGate,
        CellsWorkbookLoader loader,
        CellsSavePipeline saver,
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
    internal EditResult ApplyOps(string filePath, CellsOpsBatch batch, EditRequest options)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(options);
        string format = CellsFormats.ForOutputPath(options.OutputPath);
        if (!CellsFormats.EditIds.Contains(format, StringComparer.Ordinal))
        { throw CliErrors.FormatUnsupported(format, CellsFormats.EditIds); }
        batch = CellsOp.Catalog.Prepare(batch);
        using AtomicOutputSetWriter? transaction = options.Options.DryRun ? null
            : _saver.CreateOutputSet([Path.GetDirectoryName(options.OutputPath)!], "cells-edit", options.BackupPath);

        LicenseState licenseState = _licenseGate.EnsureApplied();
        FileWritePrecondition precondition = FileWritePrecondition.Capture(filePath);
        using InputResourceScope operationInputs = _budgets.Inputs.CreateScope();
        // The edit recalculates after its operations, or was told not to calculate at all.
        using LoadedWorkbook loaded = _loader.Open(filePath, options.Password, calculateOnOpen: false);
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
        using var importSources = new CellsImportSources(_loader, _budgets, options.OpSecrets);
        IReadOnlyList<BoundedOperationOutcome> applied = ApplyOperations(
            workbook, batch, options.Options.BestEffort, options.OpSecrets, operationInputs, importSources);
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
            Warnings = options.Options.DryRun ? EnvelopeParts.CombineWarnings(loaded.Warnings(), importSources.Warnings())
                : EnvelopeParts.CombineWarnings(
                    CombineWarnings(licenseState, loaded.Resources.CoverageWarning, loaded.EvaluationSheetSkipped, saved?.Truncated, saved?.FormulasBroken, saved?.SheetsDropped, savePlan.EncryptionWarning, saved?.EvaluationSheetAdded),
                    importSources.Warnings(),
                    EnvelopeParts.BackupWarnings(saved?.Backup)),
        };
    }

    /// <summary>Runs the batch through the SDK runner, one handler call per operation.</summary>
    private IReadOnlyList<BoundedOperationOutcome> ApplyOperations(
        Workbook workbook,
        CellsOpsBatch batch,
        bool bestEffort,
        IReadOnlyDictionary<string, string>? secrets,
        InputResourceScope inputs,
        CellsImportSources sources)
    {
        var handlers = new CellsMutationHandlers(workbook, secrets, inputs, sources);
        return BoundedOperationRunner.Run(
            CellsOp.Catalog,
            batch.Ops,
            bestEffort,
            _budgets.Deadline,
            (op, _) =>
            {
                // Charge the cells an operation writes before it writes them: a tiny op over a
                // whole sheet must fail on the budget, not after billions of assignments.
                _budgets.Consume(CellsBudgetDomains.Cells, OpsFootprint.CellCost(op), "items", "edit");
                return new AppliedOperation(handlers.Run(op) ?? 0, OpsFootprint.OutcomeTargets(op));
            },
            (op, _) => OpsFootprint.OutcomeTargets(op));
    }
}
