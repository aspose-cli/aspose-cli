using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Editing;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Operations;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Errors;
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
        ResolvedOutput output = options.Output;
        string format = output.Format.Id;
        batch = CellsOp.Catalog.Prepare(batch);
        using AtomicOutputSetWriter? transaction = options.Options.DryRun ? null
            : _saver.CreateOutputSet([output.Directory], "cells-edit", output.BackupPath);

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
        WorkbookSavePlan savePlan = WorkbookSavePlan.Create(output.Format, licenseState, options.EncryptPassword,
            loaded.IsEncrypted ? options.Password : null);
        using var importSources = new CellsImportSources(_loader, _budgets, options.OpSecrets);
        var protection = new CellsProtectionTracker();
        string[] linksBefore = LinkSources(workbook);
        (IReadOnlyList<BoundedOperationOutcome> applied, bool defaultedToActiveSheet, IReadOnlyList<Cell> formulaAnchors) = ApplyOperations(
            workbook, batch, options.Options.BestEffort, options.OpSecrets, operationInputs, importSources, protection);
        Warning? skippedSheet = loaded.SkippedSheetWarning(defaultedToActiveSheet);
        Warning? unenforced = protection.Warning(output.Format);
        Warning? relativeLinks = RelativeLinkWarning(workbook, linksBefore);
        Warning? unknownFunctions = UnknownFunctions.Warning(workbook, formulaAnchors);
        if (options.Recalculate)
        {
            workbook.CalculateFormula();
        }

        WorkbookStagedSave? saved = null;
        EditVerification? verification = null;
        if (transaction is not null)
        {
            // A batch that chose the active sheet keeps it; any other keeps the input's.
            if (!applied.Any(static outcome => outcome is { Op: "set_active_sheet", Status: OpStatuses.Ok }))
            {
                loaded.RestoreActiveSheet(savePlan);
            }

            saved = _saver.Stage(transaction, workbook, savePlan, output, precondition, verifyReopen: true);
        }

        Warning?[] editWarnings = [skippedSheet, unenforced, relativeLinks, unknownFunctions];
        IReadOnlyList<Warning>? warnings = saved is null
            ? EnvelopeParts.CombineWarnings(loaded.Warnings(editWarnings), importSources.Warnings())
            : EnvelopeParts.CombineWarnings(
                CombineWarnings(licenseState, [loaded.Resources.CoverageWarning, .. editWarnings, saved.Truncated, saved.FormulasBroken, saved.SheetsDropped, savePlan.EncryptionWarning, saved.EvaluationSheetAdded,
                    CellsEvaluation.DescribeAddedNotice(licenseState, format)]),
                importSources.Warnings(),
                EnvelopeParts.BackupWarnings(saved.Backup));
        if (transaction is not null)
        {
            if (options.Verify)
            {
                // Verification reports the warnings that make the output incomplete as issues.
                verification = _verifier.Verify(saved!.Candidate, baseline!.Path, filePath,
                    options.Password, savePlan.OutputPassword, batch, warnings);
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
            Warnings = warnings,
        };
    }

    /// <summary>
    /// Runs the batch through the SDK runner, one handler call per operation, and says whether
    /// an operation that names no sheet was applied to the active sheet. Each successful
    /// operation that changed a protected sheet or structure is recorded in
    /// <paramref name="protection"/>.
    /// </summary>
    private (IReadOnlyList<BoundedOperationOutcome> Applied, bool DefaultedToActiveSheet, IReadOnlyList<Cell> FormulaAnchors) ApplyOperations(
        Workbook workbook,
        CellsOpsBatch batch,
        bool bestEffort,
        IReadOnlyDictionary<string, Secret>? secrets,
        InputResourceScope inputs,
        CellsImportSources sources,
        CellsProtectionTracker protection)
    {
        var handlers = new CellsMutationHandlers(workbook, secrets, inputs, sources);
        IReadOnlyList<BoundedOperationOutcome> applied = BoundedOperationRunner.Run(
            CellsOp.Catalog,
            batch.Ops,
            bestEffort,
            _budgets.Deadline,
            (op, _) =>
            {
                // Charge the cells an operation writes before it writes them: a tiny op over a
                // whole sheet must fail on the budget, not after billions of assignments.
                _budgets.Consume(CellsBudgetDomains.Cells, OpsFootprint.CellCost(op), "items", "edit");
                ProtectedChange protectedTarget = CellsProtectionTracker.Observe(workbook, op);
                long affected = handlers.Run(op) ?? 0;
                protection.Record(protectedTarget);
                return new AppliedOperation(affected, OpsFootprint.OutcomeTargets(op));
            },
            (op, _) => OpsFootprint.OutcomeTargets(op));
        return (applied, handlers.DefaultedToActiveSheet, handlers.FormulaAnchors);
    }

    private static string[] LinkSources(Workbook workbook) =>
        workbook.Worksheets.ExternalLinks.Cast<ExternalLink>()
            .Where(static link => link.Type == ExternalLinkType.External)
            .Select(static link => link.DataSource)
            .ToArray();

    /// <summary>
    /// Discloses links the batch added whose target is a file name without a folder, which the
    /// output stores relative to its own folder. A link written with the full path of a file in
    /// the input's folder is stored this way too (known issue CELLS-LINK-RELATIVE-TARGET,
    /// KNOWN-ISSUES.md). A reference to a sheet the workbook does not have becomes such a link,
    /// so a target close to a sheet name is named as the likely typo.
    /// </summary>
    private static Warning? RelativeLinkWarning(Workbook workbook, string[] before)
    {
        string[] added = LinkSources(workbook)
            .Where(source => !string.IsNullOrEmpty(source)
                && Path.GetFileName(source) == source
                && !before.Contains(source, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        if (added.Length == 0)
        {
            return null;
        }

        string typos = string.Concat(added.Select(source => SheetTypo(workbook, source)));
        return new Warning
        {
            Code = CellsDiagnostics.ExternalLinkRelative,
            Message = $"The output stores the target of its new link(s) as {string.Join(", ", added)}: a file name without a folder, "
                + "relative to the output's folder. A link written with the full path of a file in the input's folder is stored this way too.",
            Hint = typos + (typos.Length > 0 ? "For a real link, keep" : "Keep")
                + " the linked workbook(s) in the same folder as the output, or bring their values in with import_range instead of a link.",
            Docs = "cells/editing",
        };
    }

    // A workbook file name has an extension; a mistyped sheet name has none.
    private static string SheetTypo(Workbook workbook, string source) =>
        Path.GetExtension(source).Length == 0
        && Mistake.Of(source, workbook.Worksheets.Cast<Worksheet>().Select(static sheet => sheet.Name)) is { Question: { } question }
            ? $"No sheet is named '{source}'. {question} Correct the formula's sheet name. "
            : string.Empty;
}
