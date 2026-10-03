using Aspose.Cli.Product.Words.Engine.Editing;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Words;
using Aspose.Words.Layout;
using Aspose.Words.Saving;
using static Aspose.Cli.Product.Words.Engine.WordsEngineSupport;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>
/// Applies one Words operation batch: opens the document, resolves every anchor before the
/// first operation runs, runs the handlers, and commits the result through the atomic writer
/// with optional save-and-reopen verification.
/// </summary>
internal sealed class WordsMutationService
{
    private readonly ILicenseGate _licenseGate;
    private readonly SafeFileWriter _writer;
    private readonly WordsDocumentLoader _loader;
    private readonly InputSource _inputs;

    internal WordsMutationService(
        ILicenseGate licenseGate,
        SafeFileWriter writer,
        WordsDocumentLoader loader,
        InputSource inputs)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _inputs = inputs ?? throw new ArgumentNullException(nameof(inputs));
    }

    /// <summary>Applies a validated operation batch and commits it atomically.</summary>
    internal WordsEditResult ApplyOps(string filePath, WordsOpsBatch batch, WordsEditRequest request)
    {
        batch = WordsOp.Catalog.Prepare(batch);
        LicenseState state = _licenseGate.EnsureApplied();
        FileWritePrecondition precondition = FileWritePrecondition.Capture(filePath);
        using LoadedDocument loaded = _loader.Open(filePath, request.Password);
        bool inputHadRevisions = loaded.Document.Revisions.Count > 0;
        bool inputWasSigned = loaded.Format.HasDigitalSignature;
        ProtectionType inputProtection = loaded.Document.ProtectionType;
        using InputResourceScope operationInputs = _inputs.CreateScope();
        ValidateRequest(request, batch);
        string format = WordsFormats.ForOutput(request.OutputPath, loaded.FormatId);
        string? outputPassword = request.EncryptPassword
            ?? (loaded.Format.IsEncrypted && WordsFormats.EncryptIds.Contains(format, StringComparer.Ordinal)
                ? request.Password : null);
        SaveOptions saveOptions = WordsSavePipeline.Options(format, outputPassword);
        if (request.Verify && !WordsFormats.IsLoad(format))
        {
            throw CliErrors.OptionInvalid("--verify", $"format '{format}' cannot be reopened as a document",
                "Use a reloadable document output when requesting semantic verification.");
        }
        SourceInfo input = InfoProjection.Source(filePath, loaded);
        FileFingerprints.EnsureUnchanged(filePath, precondition.Fingerprint, input.Fingerprint!);
        FileFingerprints.EnsureMatch(filePath, request.Options.IfMatch, input.Fingerprint!);
        IReadOnlyList<ResolvedWordsOp> resolved = WordsAnchorResolver.Resolve(loaded, batch);
        IReadOnlyList<int> originalPages = ResolveOriginalPages(loaded.Document, resolved);
        Document? baseline = request.Verify ? loaded.Document.Clone() : null;
        WordsRevisionTracking? tracking = request.TrackChanges
            ? new WordsRevisionTracking(loaded.Document, request.Author!)
            : null;
        tracking?.Start();
        var operationWarnings = new List<Warning>();
        IReadOnlyList<BoundedOperationOutcome> outcomes =
            ApplyOperations(loaded, resolved, request, operationInputs, tracking, operationWarnings);
        tracking?.Stop();

        _loader.EnsureWithinBudgets(loaded.Document, loaded.Resources);

        (OutputInfo? output, BackupInfo? backup, WordsVerification? verification) =
            Persist(
                loaded.Document,
                request,
                baseline,
                precondition,
                loaded.Resources,
                format,
                saveOptions,
                outputPassword);
        baseline?.Cleanup();
        IReadOnlyList<Warning>? outputWarnings = loaded.Format.IsEncrypted && outputPassword is null && !request.Options.DryRun
            ? [new Warning
            {
                Code = WordsDiagnostics.EncryptionRemoved,
                Message = $"The '{format}' output cannot retain the source document encryption.",
                Hint = "Use an encryption-capable document output to keep password protection.",
            }] : null;
        return new WordsEditResult
        {
            Input = input,
            Output = output,
            DryRun = request.Options.DryRun,
            Applied = outcomes,
            Backup = backup,
            PagesTouched = originalPages.Count == 0 ? null : originalPages,
            Verification = verification,
            License = EnvelopeParts.License(state),
            Warnings = EnvelopeParts.CombineWarnings(outputWarnings, EnvelopeParts.BackupWarnings(backup), operationWarnings, MutationWarnings(
                state,
                format,
                // The input's revisions are disclosed while the output still contains revisions,
                // which a Word format keeps; LOSSY_CONVERSION covers the formats that drop them.
                inputHadRevisions && loaded.Document.Revisions.Count > 0
                    && WordsFormats.WordIds.Contains(format, StringComparer.Ordinal),
                inputWasSigned,
                inputProtection,
                loaded.RemoteResourcesBlocked,
                loaded.EvaluationInputTruncated || loaded.ImportedInputTruncated)),
        };
    }

    private static void ValidateRequest(WordsEditRequest request, WordsOpsBatch batch)
    {
        if (!request.TrackChanges)
        {
            return;
        }

        string[] untracked = batch.Ops
            .Where(static op => !IsTrackable(op))
            .Select(static op => WordsOp.Catalog.NameOf(op))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (untracked.Length > 0)
        {
            throw CliErrors.OptionInvalid(
                "--track-changes",
                $"{string.Join(", ", untracked)} cannot be recorded as tracked changes",
                "Apply these operations in a separate batch without --track-changes; only content insertions and deletions are tracked.");
        }
    }

    /// <summary>
    /// Whether Word can record the operation as tracked changes. Aspose.Words tracks the
    /// insertion and deletion of content only; formatting, styles, lists, page setup,
    /// properties, protection, merges, field updates, header replacement and section
    /// structure would change silently, and resolving revisions is not itself an edit. Comments
    /// are review annotations rather than revisions, so their operations apply untracked.
    /// </summary>
    private static bool IsTrackable(WordsOp op) => op is ReplaceTextOp or SetTextOp or InsertParagraphsOp
        or InsertMarkdownOp or DeleteBlocksOp or InsertBreakOp { Kind: "page" } or InsertImageOp or InsertTableOp
        or SetTableCellOp or RepeatTableRowOp or InsertTocOp or InsertBookmarkOp or InsertHyperlinkOp or InsertFieldOp
        or AddCommentOp or RemoveCommentsOp or AppendDocumentOp;

    private IReadOnlyList<BoundedOperationOutcome> ApplyOperations(
        LoadedDocument loaded,
        IReadOnlyList<ResolvedWordsOp> resolved,
        WordsEditRequest request,
        InputResourceScope operationInputs,
        WordsRevisionTracking? tracking,
        List<Warning> warnings)
    {
        return BoundedOperationRunner.Run(
            WordsOp.Catalog,
            resolved.Select(static item => item.Op).ToArray(),
            request.Options.BestEffort,
            deadline: null,
            (_, index) => new AppliedOperation(
                new WordsMutationHandlers(loaded, resolved[index], _loader, _inputs, operationInputs, request.OpSecrets, tracking, warnings).Run(),
                resolved[index].Targets),
            (_, index) => resolved[index].Targets);
    }

    private (
        OutputInfo? Output,
        BackupInfo? Backup,
        WordsVerification? Verification) Persist(
        Document document,
        WordsEditRequest request,
        Document? baseline,
        FileWritePrecondition precondition,
        LocalDocumentResourceLoader resources,
        string format,
        SaveOptions saveOptions,
        string? outputPassword)
    {
        OutputInfo? output = null;
        BackupInfo? backup = null;
        WordsVerification? verification = null;
        if (!request.Options.DryRun)
        {
            using var transaction = new AtomicOutputSetWriter(_writer, Path.GetDirectoryName(request.OutputPath)!, "words-edit");
            StagedOutput write = transaction.Stage(
                request.OutputPath,
                request.Overwrite,
                request.BackupPath,
                precondition,
                temp =>
                {
                    try { document.Save(temp, saveOptions); }
                    finally { resources.ThrowIfFailed(); }
                    if (!request.Verify && WordsFormats.IsLoad(format))
                    {
                        using LoadedDocument reopened = _loader.OpenPublishedCandidate(temp, outputPassword);
                    }
                });
            output = new OutputInfo { Path = request.OutputPath, Format = format, SizeBytes = write.SizeBytes };
            backup = write.Backup;

            if (request.Verify)
            {
                var expected = new ExpectedDocumentState(
                    document.Range.Fields.Count,
                    document.Revisions.Count,
                    WordsProtection.ToMode(document.ProtectionType));
                verification = write.Read(
                    candidate => Verify(candidate, format, outputPassword, baseline!, expected));
            }
            transaction.Commit();
        }
        return (output, backup, verification);
    }

    private static IReadOnlyList<int> ResolveOriginalPages(Document document, IReadOnlyList<ResolvedWordsOp> operations)
    {
        document.UpdatePageLayout();
        var collector = new LayoutCollector(document);
        return operations.SelectMany(static item => item.Nodes)
            .Where(static node => node.ParentNode is not null)
            .Select(collector.GetStartPageIndex)
            .Where(static page => page > 0)
            .Distinct()
            .Order()
            .ToArray();
    }

    /// <summary>
    /// Reports save-and-reopen evidence for the staged candidate. A comparison that
    /// finds no body change is evidence, not a fault: whether an operation did
    /// anything is already answered, authoritatively, by its own outcome.
    /// </summary>
    private WordsVerification Verify(
        string candidatePath,
        string format,
        string? outputPassword,
        Document baseline,
        ExpectedDocumentState expected)
    {
        var issues = new List<VerificationIssue>();
        using LoadedDocument reopened = _loader.OpenPublishedCandidate(
            candidatePath,
            outputPassword);
        int fieldCount = reopened.Document.Range.Fields.Count;
        int revisionCount = reopened.Document.Revisions.Count;
        string protection = WordsProtection.ToMode(reopened.Document.ProtectionType);
        if (fieldCount != expected.FieldCount)
        {
            issues.Add(VerificationIssue.Of(
                WordsDiagnostics.FieldCountChanged,
                $"Field count changed during save/reopen: expected {expected.FieldCount}, found {fieldCount}.",
                hint: KeepStateHint("fields", format)));
        }

        if (revisionCount != expected.RevisionCount)
        {
            issues.Add(VerificationIssue.Of(
                WordsDiagnostics.RevisionCountChanged,
                $"Revision count changed during save/reopen: expected {expected.RevisionCount}, found {revisionCount}.",
                hint: KeepStateHint("tracked revisions", format)));
        }

        if (!string.Equals(protection, expected.Protection, StringComparison.Ordinal))
        {
            issues.Add(VerificationIssue.Of(
                WordsDiagnostics.ProtectionChanged,
                $"Protection changed during save/reopen: expected {expected.Protection}, found {protection}.",
                hint: KeepStateHint("protection", format)));
        }

        Document comparisonBaseline = baseline.Clone();
        Document comparisonOutput = reopened.Document.Clone();
        comparisonBaseline.AcceptAllRevisions();
        comparisonOutput.AcceptAllRevisions();
        comparisonBaseline.Compare(
            comparisonOutput,
            "Aspose CLI",
            new DateTime(2000, 1, 1));
        bool semanticChanges = comparisonBaseline.Revisions.Count > 0;
        comparisonBaseline.Cleanup();
        comparisonOutput.Cleanup();

        return new WordsVerification
        {
            Ok = issues.Count == 0,
            Issues = issues,
            SemanticChangesDetected = semanticChanges,
            FieldCount = fieldCount,
            RevisionCount = revisionCount,
            Protection = protection,
        };
    }

    /// <summary>
    /// Advises a Word format only when the output is not one; otherwise the state was lost by
    /// a save that already used a Word format, and repeating it would not help.
    /// </summary>
    internal static string KeepStateHint(string state, string format) =>
        WordsFormats.WordIds.Contains(format, StringComparer.Ordinal)
            ? $"The {state} did not survive save and reopen in {format}; check the output with 'aspose-cli words inspect' before relying on it."
            : $"{format} may not keep {state}; save to docx or another Word format and verify again.";

    private static IReadOnlyList<Warning>? MutationWarnings(
        LicenseState state,
        string format,
        bool revisionsKept,
        bool inputWasSigned,
        ProtectionType inputProtection,
        int remoteResourcesBlocked,
        bool evaluationInputTruncated)
    {
        var extra = new List<Warning>();
        if (revisionsKept)
        {
            extra.Add(new Warning { Code = WordsDiagnostics.TrackedChangesPresent, Message = "The input has tracked changes, and the output still contains tracked changes.", Hint = "Disclose them and accept or reject only when explicitly requested." });
        }

        if (inputWasSigned)
        {
            extra.Add(new Warning { Code = WarningCodes.SignatureInvalidated, Message = "Editing invalidates the document's digital signature.", Hint = "Re-sign the produced document after review." });
        }

        if (inputProtection != ProtectionType.NoProtection)
        {
            // Editing restrictions guide Word's UI; they are not encryption and do not bind the SDK.
            extra.Add(new Warning
            {
                Code = WarningCodes.ProtectionNotEnforced,
                Message = $"The input has {WordsProtection.ToMode(inputProtection)} editing restrictions; the edit was applied through them.",
                Hint = WordsFormats.WordIds.Contains(format, StringComparer.Ordinal)
                    ? "Confirm the change is authorized. The output keeps the restrictions unless the batch changed them with protect or unprotect."
                    : $"Confirm the change is authorized. A {format} output may not keep the restrictions; save to docx or another Word format to keep them.",
            });
        }

        if (LossyConversion(format) is { } lossy)
        {
            extra.Add(lossy);
        }

        if (LocalDocumentResourceLoader.OmissionWarning(remoteResourcesBlocked) is { } omitted)
        {
            extra.Add(omitted);
        }

        if (evaluationInputTruncated)
        {
            extra.Add(EvaluationTruncated);
        }

        return EnvelopeParts.CombineWarnings(EnvelopeParts.OutputWarnings(state), extra);
    }

    private sealed record ExpectedDocumentState(int FieldCount, int RevisionCount, string Protection);
}
