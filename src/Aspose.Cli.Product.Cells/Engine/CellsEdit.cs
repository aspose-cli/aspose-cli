using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Editing;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Operations;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary><c>cells edit</c>: opens a workbook, applies an ops batch atomically and publishes the result.</summary>
internal static class CellsEdit
{
    public static CellsEditResult Run(CellsSession session, CellsEditRequest request)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.Input);
        ArgumentNullException.ThrowIfNull(request.Batch);
        string filePath = request.Input;
        ResolvedOutput output = request.Output;
        CellsOpsBatch batch = CellsOp.Catalog.Prepare(request.Batch);

        LicenseState licenseState = session.Outputs.License;
        FileWritePrecondition precondition = FileWritePrecondition.Capture(filePath);
        using InputResourceScope operationInputs = session.Budgets.Inputs.CreateScope();
        // The edit recalculates after its operations, or was told not to calculate at all.
        using LoadedWorkbook loaded = session.Loader.Open(filePath, request.Password, calculateOnOpen: false);
        Workbook workbook = loaded.Workbook;
        InputInfo input = precondition.Admit(
            CellsEngineFormats.IdOf(loaded.SourceFormat), loaded.Protection, request.Options.IfMatch);

        using CellsEditBaseline? baseline = request.Verify
            ? CellsEditBaseline.Capture(filePath, precondition, session.Budgets) : null;
        WorkbookSavePlan savePlan = WorkbookSavePlan.Create(output.Format, licenseState,
            OutputProtection.For(output.Format, request.EncryptPassword, loaded.Protection, request.Password));
        using var importSources = new CellsImportSources(session.Loader, session.Budgets, request.OpSecrets);
        var protection = new CellsProtectionTracker();
        string[] linksBefore = LinkSources(workbook);
        // The sheet an operation that names none addresses, as the batch finds it.
        string activeSheet = Sheets.Resolve(workbook, sheetName: null).Name;
        var model = new CellsEditModel(session.Budgets, workbook, request.OpSecrets, operationInputs, importSources, protection);
        AppliedBatch batchOutcome = BoundedOperationRunner.Run(
            CellsOp.Catalog, batch.Ops, request.Options.BestEffort, session.Budgets.Deadline, model);
        IReadOnlyList<BoundedOperationOutcome> applied = batchOutcome.Outcomes;
        bool defaultedToActiveSheet = model.DefaultedToActiveSheet;
        IReadOnlyList<Cell> formulaAnchors = model.FormulaAnchors;
        Warning? skippedSheet = loaded.SkippedSheetWarning(defaultedToActiveSheet, "Give each operation its \"sheet\", the sheet's name from inspect, to address another sheet.");
        Warning? unenforced = protection.Warning(output.Format);
        Warning? relativeLinks = RelativeLinkWarning(workbook, linksBefore);
        Warning? unknownFunctions = UnknownFunctions.Warning(workbook, formulaAnchors);
        if (request.Recalculate)
        {
            workbook.CalculateFormula();
        }

        if (!request.Options.DryRun)
        {
            // A batch that chose the active sheet keeps it; any other keeps the input's.
            string[] activating = [.. batch.Ops.OfType<SetActiveSheetOp>().Select(static op => op.Id!)];
            if (!applied.Any(outcome => outcome.Status == OpStatuses.Ok && activating.Contains(outcome.Id)))
            {
                loaded.RestoreActiveSheet(savePlan);
            }
        }

        Warning?[] editWarnings = [.. batchOutcome.Warnings, skippedSheet, unenforced, relativeLinks, unknownFunctions];
        // The save records what the format's limits lost; the verification reads it back.
        WorkbookSaveLoss? loss = null;
        IReadOnlyList<Warning>? WrittenWarnings() => EnvelopeParts.CombineWarnings(
            EnvelopeParts.CombineWarnings([.. loaded.Resources.Warnings, .. editWarnings, .. loss!.Warnings]),
            importSources.Warnings());
        EditPublication<CellsEditVerification> publication = session.Outputs.PublishEdit<CellsEditVerification>(
            output,
            workbook,
            precondition,
            request.Options.DryRun,
            savePlan.Encrypts,
            temp =>
            {
                loss = new CellsSavePipeline(session.Outputs).ProduceReporting(workbook, savePlan, temp);
                using LoadedWorkbook reopened = session.Loader.OpenPublishedCandidate(temp, savePlan.Protection.Password);
                _ = reopened.Workbook.Worksheets.Count;
            },
            // Verification reports the warnings that make the output incomplete as issues.
            request.Verify ? candidate => new CellsEditVerifier(session.Loader, session.Budgets).Verify(candidate, baseline!.Path, filePath,
                request.Password, savePlan.Protection.Password, batch, activeSheet, applied, WrittenWarnings()) : null,
            "cells-edit");
        IReadOnlyList<Warning>? warnings = request.Options.DryRun
            ? EnvelopeParts.CombineWarnings(loaded.Warnings(editWarnings), importSources.Warnings())
            : EnvelopeParts.CombineWarnings(WrittenWarnings(), publication.BackupWarnings);

        return new CellsEditResult
        {
            Input = input,
            Output = publication.Output,
            DryRun = request.Options.DryRun,
            Checked = request.Options.Checked,
            Recalculated = request.Recalculate,
            Applied = applied,
            Backup = publication.Backup,
            Verification = publication.Verification,
            License = EnvelopeParts.License(licenseState),
            Warnings = warnings,
        };
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
        HasNoExtension(source)
        && Mistake.Of(source, workbook.Worksheets.Cast<Worksheet>().Select(static sheet => sheet.Name)) is { Question: { } question }
            ? $"No sheet is named '{source}'. {question} Correct the formula's sheet name. "
            : string.Empty;

    /// <summary>
    /// Whether a link source names no file extension: no dot after its last folder separator,
    /// or only a final one, as in <c>Sheet.</c>. This tells a sheet name from a file name; it
    /// decides no format.
    /// </summary>
    internal static bool HasNoExtension(string source)
    {
        int dot = source.LastIndexOf('.');
        return dot < 0 || dot == source.Length - 1 || source.IndexOfAny(['\\', '/'], dot) >= 0;
    }
}
