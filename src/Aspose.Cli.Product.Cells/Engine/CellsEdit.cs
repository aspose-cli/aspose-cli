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

/// <summary><c>cells edit</c>: opens a workbook, applies an ops batch atomically and publishes the result.</summary>
internal static class CellsEdit
{
    public static EditResult Run(CellsSession session, EditRequest request)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.Input);
        ArgumentNullException.ThrowIfNull(request.Batch);
        string filePath = request.Input;
        var saver = new CellsSavePipeline(session.Outputs, session.Loader);
        ResolvedOutput output = request.Output;
        string format = output.Format.Id;
        CellsOpsBatch batch = CellsOp.Catalog.Prepare(request.Batch);
        using OutputSet<Workbook>? transaction = request.Options.DryRun ? null
            : saver.CreateOutputSet([output.Directory], "cells-edit", output.BackupPath);

        LicenseState licenseState = session.Outputs.License;
        FileWritePrecondition precondition = FileWritePrecondition.Capture(filePath);
        using InputResourceScope operationInputs = session.Budgets.Inputs.CreateScope();
        // The edit recalculates after its operations, or was told not to calculate at all.
        using LoadedWorkbook loaded = session.Loader.Open(filePath, request.Password, calculateOnOpen: false);
        Workbook workbook = loaded.Workbook;
        SourceInfo input = BuildSource(filePath, loaded);
        FileFingerprints.EnsureUnchanged(filePath, precondition.Fingerprint, input.Fingerprint!);
        FileFingerprints.EnsureMatch(
            filePath,
            request.Options.IfMatch,
            input.Fingerprint!);

        using CellsEditBaseline? baseline = request.Verify
            ? CellsEditBaseline.Capture(filePath, precondition, session.Budgets) : null;
        WorkbookSavePlan savePlan = WorkbookSavePlan.Create(output.Format, licenseState, request.EncryptPassword,
            loaded.IsEncrypted ? request.Password : null);
        using var importSources = new CellsImportSources(session.Loader, session.Budgets, request.OpSecrets);
        var protection = new CellsProtectionTracker();
        string[] linksBefore = LinkSources(workbook);
        (IReadOnlyList<BoundedOperationOutcome> applied, bool defaultedToActiveSheet, IReadOnlyList<Cell> formulaAnchors) = ApplyOperations(
            session.Budgets, workbook, batch, request.Options.BestEffort, request.OpSecrets, operationInputs, importSources, protection);
        Warning? skippedSheet = loaded.SkippedSheetWarning(defaultedToActiveSheet);
        Warning? unenforced = protection.Warning(output.Format);
        Warning? relativeLinks = RelativeLinkWarning(workbook, linksBefore);
        Warning? unknownFunctions = UnknownFunctions.Warning(workbook, formulaAnchors);
        if (request.Recalculate)
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

            saved = saver.Stage(transaction, workbook, savePlan, output, precondition, verifyReopen: true);
        }

        Warning?[] editWarnings = [skippedSheet, unenforced, relativeLinks, unknownFunctions];
        IReadOnlyList<Warning>? warnings = saved is null
            ? EnvelopeParts.CombineWarnings(loaded.Warnings(editWarnings), importSources.Warnings())
            : EnvelopeParts.CombineWarnings(
                EnvelopeParts.CombineWarnings([loaded.Resources.CoverageWarning, .. editWarnings, saved.Truncated, saved.FormulasBroken, saved.SheetsDropped, savePlan.EncryptionWarning]),
                importSources.Warnings(),
                EnvelopeParts.BackupWarnings(saved.Backup));
        if (transaction is not null)
        {
            if (request.Verify)
            {
                // Verification reports the warnings that make the output incomplete as issues.
                verification = new CellsEditVerifier(session.Loader, session.Budgets).Verify(saved!.Candidate, baseline!.Path, filePath,
                    request.Password, savePlan.OutputPassword, batch, warnings);
            }
            transaction.Commit();
        }

        return new EditResult
        {
            Input = input,
            Output = saved?.Output,
            DryRun = request.Options.DryRun,
            Recalculated = request.Recalculate,
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
    private static (IReadOnlyList<BoundedOperationOutcome> Applied, bool DefaultedToActiveSheet, IReadOnlyList<Cell> FormulaAnchors) ApplyOperations(
        ResourceBudgetLedger budgets,
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
            budgets.Deadline,
            (op, _) =>
            {
                // Charge the cells an operation writes before it writes them: a tiny op over a
                // whole sheet must fail on the budget, not after billions of assignments.
                budgets.Consume(CellsBudgetDomains.Cells, OpsFootprint.CellCost(op), "items", "edit");
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
        return new Warning(CellsDiagnostics.ExternalLinkRelative, $"The output stores the target of its new link(s) as {string.Join(", ", added)}: a file name without a folder, "
                + "relative to the output's folder. A link written with the full path of a file in the input's folder is stored this way too.")
        {
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
